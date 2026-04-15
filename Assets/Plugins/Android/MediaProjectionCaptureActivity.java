/*
 * MediaProjectionCaptureActivity.java
 * ------------------------------------------------------------
 * VR Voice Claude — Phase 3 Sprint 1 (Task 1.7)
 *
 * Role
 *   Captures the Quest 3 composited framebuffer (real-world passthrough +
 *   any virtual app / panel overlays the user sees — YouTube, browser,
 *   Meta panels, etc.) via the Android MediaProjection API and exposes
 *   the latest frame to Unity C# as JPEG bytes via JNI (AndroidJavaClass).
 *
 *   Meta's official Passthrough Camera API docs point to MediaProjection
 *   as the right tool when the goal is "represent what the user is seeing,
 *   INCLUDING the User Interface" — which is exactly what Claude needs
 *   to see. The headset cameras alone don't include virtual overlays.
 *
 * Unity 6 / 6000.4.2f1 requirement
 *   This class MUST extend com.unity3d.player.UnityPlayerGameActivity
 *   (the new GameActivity-based player), NOT the legacy UnityPlayerActivity.
 *   Subclassing the wrong base class hard-crashes on launch. This is a
 *   frozen Phase 2 decision for this app — see AndroidManifest.xml which
 *   already references UnityPlayerGameActivity and BaseUnityGameActivityTheme.
 *
 * Android 14 / API 34 gotcha
 *   As of API 34, you MUST start a foreground service of type
 *   FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION BEFORE calling
 *   MediaProjectionManager.getMediaProjection(resultCode, data).
 *   Skipping that step throws SecurityException. The sequence is:
 *     1. User consents via the system dialog (onActivityResult RESULT_OK)
 *     2. startForegroundService(MediaProjectionForegroundService)
 *     3. The service calls startForeground(...) with type mediaProjection
 *     4. Service notifies back -> getMediaProjection(...) -> createVirtualDisplay
 *
 *   Additionally, API 34 requires a MediaProjection.Callback registered
 *   on the projection (onStop -> stopCapture) or getMediaProjection throws.
 *
 * Unity-callable static API (pull-style, no UnitySendMessage)
 *   static void   requestProjectionPermission()
 *   static boolean isReady()
 *   static byte[] getLatestFrameJpeg(int maxDim, int quality)
 *   static void   stopCapture()
 *   static String getLastError()
 */
package com.gbbraga.vrvoiceclaude;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.PixelFormat;
import android.hardware.display.DisplayManager;
import android.hardware.display.VirtualDisplay;
import android.media.Image;
import android.media.ImageReader;
import android.media.projection.MediaProjection;
import android.media.projection.MediaProjectionManager;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.HandlerThread;
import android.os.Looper;
import android.util.DisplayMetrics;
import android.util.Log;
import android.view.Display;

import com.unity3d.player.UnityPlayerGameActivity;

import java.io.ByteArrayOutputStream;
import java.nio.ByteBuffer;

public class MediaProjectionCaptureActivity extends UnityPlayerGameActivity {

    private static final String TAG = "VRVoiceClaudeMP";
    private static final int REQUEST_CODE_MEDIA_PROJECTION = 0xCAFE;
    private static final int TARGET_MAX_DIM = 1280; // ~720p downscale

    // --- Singleton hooks so the static Unity-callable methods can reach the
    //     current Activity instance. The game has exactly ONE activity.
    private static MediaProjectionCaptureActivity sInstance;

    // --- Shared state guarded by LOCK. All access to latestFrame must lock.
    private static final Object LOCK = new Object();
    private static Bitmap latestFrame;             // guarded by LOCK
    private static volatile String lastError;     // volatile read is fine
    private static volatile boolean capturing;    // volatile read is fine

    // --- Pending consent payload (survives between permission step and
    //     foreground-service-started callback).
    private static int sPendingResultCode;
    private static Intent sPendingData;

    // --- Projection resources (touched only on the main thread).
    private MediaProjectionManager projectionManager;
    private MediaProjection mediaProjection;
    private VirtualDisplay virtualDisplay;
    private ImageReader imageReader;
    private HandlerThread captureThread;
    private Handler captureHandler;
    private MediaProjection.Callback projectionCallback;

    // volatile: written on the main thread in beginProjectionCapture, read
    // on the capture HandlerThread in onImageAvailable. The Looper handoff
    // likely establishes happens-before, but volatile makes it explicit.
    private volatile int captureWidth;
    private volatile int captureHeight;
    private int captureDensity;

    // ===================================================================
    // Activity lifecycle
    // ===================================================================

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        sInstance = this;
        projectionManager =
                (MediaProjectionManager) getSystemService(Context.MEDIA_PROJECTION_SERVICE);
        Log.i(TAG, "MediaProjectionCaptureActivity onCreate");
    }

    @Override
    protected void onDestroy() {
        try {
            stopCaptureInternal();
        } catch (Throwable t) {
            Log.w(TAG, "onDestroy stopCaptureInternal threw", t);
        }
        if (sInstance == this) {
            sInstance = null;
        }
        super.onDestroy();
    }

    // ===================================================================
    // Static Unity-callable API
    // ===================================================================

    /**
     * Kick off the system MediaProjection consent dialog. Safe to call
     * multiple times — no-op if already capturing or if a consent dialog
     * is already in flight. Always hops to the UI thread; the reentrancy
     * gate is evaluated INSIDE the UI-thread runnable to close the race
     * where two rapid callers both pass an early capturing==false check
     * before either reaches startActivityForResult.
     */
    public static void requestProjectionPermission() {
        final MediaProjectionCaptureActivity act = sInstance;
        if (act == null) {
            lastError = "Activity instance not ready";
            Log.e(TAG, lastError);
            return;
        }
        act.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                // Reentrancy gate — must be on the UI thread so it is
                // serialized with onActivityResult and with other callers.
                if (capturing) {
                    Log.i(TAG, "requestProjectionPermission: already capturing, no-op");
                    return;
                }
                if (sPendingData != null) {
                    Log.i(TAG, "requestProjectionPermission: consent flow already in flight, no-op");
                    return;
                }
                try {
                    if (act.projectionManager == null) {
                        act.projectionManager = (MediaProjectionManager)
                                act.getSystemService(Context.MEDIA_PROJECTION_SERVICE);
                    }
                    Intent intent = act.projectionManager.createScreenCaptureIntent();
                    act.startActivityForResult(intent, REQUEST_CODE_MEDIA_PROJECTION);
                    Log.i(TAG, "Consent dialog launched");
                } catch (Throwable t) {
                    lastError = "requestProjectionPermission failed: "
                            + t.getClass().getSimpleName() + ": " + String.valueOf(t.getMessage());
                    Log.e(TAG, lastError, t);
                }
            }
        });
    }

    /** @return true once at least one frame has been captured. */
    public static boolean isReady() {
        synchronized (LOCK) {
            return latestFrame != null;
        }
    }

    /**
     * Snapshot the latest captured frame as JPEG bytes. Optionally
     * downscales so max(width,height) &lt;= maxDim. Returns null if no
     * frame has been captured yet. Does NOT recycle the latestFrame.
     */
    public static byte[] getLatestFrameJpeg(int maxDim, int quality) {
        Bitmap snapshot;
        synchronized (LOCK) {
            if (latestFrame == null) {
                return null;
            }
            // Duplicate the bitmap so we can release the lock before the
            // (slow) JPEG encode and before any scaling. The copy shares
            // no state with latestFrame, which remains alive for next call.
            snapshot = latestFrame.copy(Bitmap.Config.ARGB_8888, false);
        }
        if (snapshot == null) {
            return null;
        }
        try {
            int w = snapshot.getWidth();
            int h = snapshot.getHeight();
            int longest = Math.max(w, h);
            if (maxDim > 0 && longest > maxDim) {
                float scale = (float) maxDim / (float) longest;
                int nw = Math.max(1, Math.round(w * scale));
                int nh = Math.max(1, Math.round(h * scale));
                Bitmap scaled = Bitmap.createScaledBitmap(snapshot, nw, nh, true);
                if (scaled != snapshot) {
                    snapshot.recycle();
                    snapshot = scaled;
                }
            }
            int q = Math.max(1, Math.min(100, quality));
            ByteArrayOutputStream baos = new ByteArrayOutputStream(64 * 1024);
            snapshot.compress(Bitmap.CompressFormat.JPEG, q, baos);
            return baos.toByteArray();
        } catch (Throwable t) {
            lastError = "getLatestFrameJpeg failed: "
                    + t.getClass().getSimpleName() + ": " + String.valueOf(t.getMessage());
            Log.e(TAG, lastError, t);
            return null;
        } finally {
            if (snapshot != null && !snapshot.isRecycled()) {
                snapshot.recycle();
            }
        }
    }

    /** Idempotent stop. Tears down VirtualDisplay, ImageReader, projection, FG service. */
    public static void stopCapture() {
        final MediaProjectionCaptureActivity act = sInstance;
        if (act == null) {
            // Still clear latestFrame so isReady() reports false.
            synchronized (LOCK) {
                if (latestFrame != null) {
                    latestFrame.recycle();
                    latestFrame = null;
                }
            }
            capturing = false;
            return;
        }
        act.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                act.stopCaptureInternal();
            }
        });
    }

    /** @return the last error string captured by the plugin, or null. */
    public static String getLastError() {
        return lastError;
    }

    // ===================================================================
    // onActivityResult — consent -> foreground service -> projection
    // ===================================================================

    @Override
    public void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode == REQUEST_CODE_MEDIA_PROJECTION) {
            if (resultCode == Activity.RESULT_OK && data != null) {
                Log.i(TAG, "Consent granted, starting foreground service");
                sPendingResultCode = resultCode;
                sPendingData = data;
                // API 34: start a mediaProjection-typed foreground service
                // BEFORE calling getMediaProjection, else SecurityException.
                // The service will call us back on its own thread once
                // startForeground has actually happened — only then is it
                // safe to call getMediaProjection.
                MediaProjectionForegroundService.setReadyCallback(new Runnable() {
                    @Override
                    public void run() {
                        // Hop to main thread — projection & VirtualDisplay
                        // setup is cleaner there.
                        runOnUiThread(new Runnable() {
                            @Override
                            public void run() {
                                beginProjectionCapture(sPendingResultCode, sPendingData);
                                sPendingData = null;
                            }
                        });
                    }
                });
                Intent svcIntent = new Intent(this, MediaProjectionForegroundService.class);
                try {
                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                        startForegroundService(svcIntent);
                    } else {
                        startService(svcIntent);
                    }
                } catch (Throwable t) {
                    lastError = "startForegroundService failed: "
                            + t.getClass().getSimpleName() + ": " + String.valueOf(t.getMessage());
                    Log.e(TAG, lastError, t);
                    MediaProjectionForegroundService.setReadyCallback(null);
                    sPendingData = null;
                }
            } else {
                lastError = "Consent denied or cancelled (resultCode=" + resultCode + ")";
                Log.w(TAG, lastError);
            }
        }
        // Always call super so Unity's own activity result handling still runs.
        super.onActivityResult(requestCode, resultCode, data);
    }

    // ===================================================================
    // Projection setup (main thread)
    // ===================================================================

    private void beginProjectionCapture(int resultCode, Intent data) {
        try {
            mediaProjection = projectionManager.getMediaProjection(resultCode, data);
            if (mediaProjection == null) {
                throw new IllegalStateException("getMediaProjection returned null");
            }

            // Compute downscaled capture size preserving aspect. Quest 3's
            // per-eye display is high-res; we clamp max dim to 1280 to keep
            // ImageReader bandwidth + JPEG encode snappy.
            DisplayMetrics metrics = new DisplayMetrics();
            Display display = ((DisplayManager)
                    getSystemService(Context.DISPLAY_SERVICE))
                    .getDisplay(Display.DEFAULT_DISPLAY);
            if (display != null) {
                display.getRealMetrics(metrics);
            } else {
                getWindowManager().getDefaultDisplay().getRealMetrics(metrics);
            }
            int srcW = metrics.widthPixels;
            int srcH = metrics.heightPixels;
            captureDensity = metrics.densityDpi;
            int longest = Math.max(srcW, srcH);
            if (longest > TARGET_MAX_DIM) {
                float s = (float) TARGET_MAX_DIM / (float) longest;
                captureWidth = Math.max(1, Math.round(srcW * s));
                captureHeight = Math.max(1, Math.round(srcH * s));
            } else {
                captureWidth = srcW;
                captureHeight = srcH;
            }
            // ImageReader requires even dimensions for some codecs; round.
            if ((captureWidth & 1) != 0) captureWidth++;
            if ((captureHeight & 1) != 0) captureHeight++;
            Log.i(TAG, "Capture size: " + captureWidth + "x" + captureHeight
                    + " (src " + srcW + "x" + srcH + ")");

            imageReader = ImageReader.newInstance(
                    captureWidth,
                    captureHeight,
                    PixelFormat.RGBA_8888,
                    2);

            captureThread = new HandlerThread("vrvoiceclaude-capture");
            captureThread.start();
            captureHandler = new Handler(captureThread.getLooper());

            // Register projection callback BEFORE createVirtualDisplay.
            // API 34 requires this or getMediaProjection-era usage throws.
            projectionCallback = new MediaProjection.Callback() {
                @Override
                public void onStop() {
                    Log.i(TAG, "MediaProjection.Callback onStop");
                    if (sInstance != null) {
                        sInstance.runOnUiThread(new Runnable() {
                            @Override
                            public void run() {
                                stopCaptureInternal();
                            }
                        });
                    }
                }
            };
            mediaProjection.registerCallback(projectionCallback, new Handler(Looper.getMainLooper()));

            imageReader.setOnImageAvailableListener(onImageAvailable, captureHandler);

            virtualDisplay = mediaProjection.createVirtualDisplay(
                    "vrvoiceclaude-capture",
                    captureWidth,
                    captureHeight,
                    captureDensity,
                    DisplayManager.VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR,
                    imageReader.getSurface(),
                    null,
                    captureHandler);

            capturing = true;
            Log.i(TAG, "VirtualDisplay created, capture running");
        } catch (Throwable t) {
            lastError = "beginProjectionCapture failed: "
                    + t.getClass().getSimpleName() + ": " + String.valueOf(t.getMessage());
            Log.e(TAG, lastError, t);
            stopCaptureInternal();
        }
    }

    // ===================================================================
    // OnImageAvailable — RGBA_8888 -> ARGB_8888 Bitmap with row stride
    // ===================================================================

    private final ImageReader.OnImageAvailableListener onImageAvailable =
            new ImageReader.OnImageAvailableListener() {
        @Override
        public void onImageAvailable(ImageReader reader) {
            Image image = null;
            try {
                image = reader.acquireLatestImage();
                if (image == null) {
                    return;
                }
                Image.Plane[] planes = image.getPlanes();
                if (planes.length == 0) {
                    return;
                }
                Image.Plane plane = planes[0];
                ByteBuffer buffer = plane.getBuffer();
                int pixelStride = plane.getPixelStride();    // normally 4
                int rowStride = plane.getRowStride();        // can be > width * 4
                int rowPadding = rowStride - pixelStride * captureWidth;

                // Allocate a Bitmap padded to rowStride width, then crop.
                // This is the canonical way to handle non-zero rowPadding
                // from ImageReader + RGBA_8888 — a naive copyPixelsFromBuffer
                // would splat the padding bytes into pixel data.
                int paddedWidth = captureWidth + rowPadding / pixelStride;
                Bitmap padded = Bitmap.createBitmap(
                        paddedWidth,
                        captureHeight,
                        Bitmap.Config.ARGB_8888);
                padded.copyPixelsFromBuffer(buffer);

                Bitmap cropped;
                if (paddedWidth == captureWidth) {
                    cropped = padded;
                } else {
                    cropped = Bitmap.createBitmap(padded, 0, 0, captureWidth, captureHeight);
                    if (cropped != padded) {
                        padded.recycle();
                    }
                }

                // Swap the latest frame atomically and recycle the old one.
                synchronized (LOCK) {
                    Bitmap old = latestFrame;
                    latestFrame = cropped;
                    if (old != null && old != cropped && !old.isRecycled()) {
                        old.recycle();
                    }
                }
            } catch (Throwable t) {
                lastError = "onImageAvailable failed: "
                        + t.getClass().getSimpleName() + ": " + String.valueOf(t.getMessage());
                Log.e(TAG, lastError, t);
            } finally {
                if (image != null) {
                    try {
                        image.close();
                    } catch (Throwable ignore) { }
                }
            }
        }
    };

    // ===================================================================
    // Stop / teardown (idempotent)
    // ===================================================================

    private void stopCaptureInternal() {
        capturing = false;
        // Step 1: release VirtualDisplay first — stops the image producer
        // so the ImageReader stops receiving new frames.
        try {
            if (virtualDisplay != null) {
                virtualDisplay.release();
                virtualDisplay = null;
            }
        } catch (Throwable t) {
            Log.w(TAG, "virtualDisplay.release threw", t);
        }
        // Step 2: detach the OnImageAvailableListener. Note that this call
        // does NOT wait for an in-flight callback on the capture thread,
        // so we must drain that thread before recycling latestFrame.
        try {
            if (imageReader != null) {
                imageReader.setOnImageAvailableListener(null, null);
            }
        } catch (Throwable t) {
            Log.w(TAG, "imageReader.setOnImageAvailableListener(null) threw", t);
        }
        // Step 3: drain the capture HandlerThread. quitSafely() lets any
        // already-enqueued onImageAvailable runnable finish, then the
        // looper exits. join(500) guarantees we observe its completion
        // before we touch latestFrame. This closes the I5 race where the
        // listener could write a fresh bitmap AFTER we recycled the old one.
        HandlerThread threadToJoin = captureThread;
        captureThread = null;
        captureHandler = null;
        if (threadToJoin != null) {
            try {
                threadToJoin.quitSafely();
            } catch (Throwable t) {
                Log.w(TAG, "captureThread.quitSafely threw", t);
            }
            try {
                threadToJoin.join(500);
            } catch (InterruptedException ie) {
                Thread.currentThread().interrupt();
                Log.w(TAG, "captureThread.join interrupted", ie);
            } catch (Throwable t) {
                Log.w(TAG, "captureThread.join threw", t);
            }
        }
        // Step 4: now that no capture callback can be running, it's safe
        // to recycle latestFrame. Under the lock so no concurrent
        // getLatestFrameJpeg caller gets torn state.
        synchronized (LOCK) {
            if (latestFrame != null) {
                latestFrame.recycle();
                latestFrame = null;
            }
        }
        // Step 5: close ImageReader, release MediaProjection, stop FG service.
        try {
            if (imageReader != null) {
                imageReader.close();
                imageReader = null;
            }
        } catch (Throwable t) {
            Log.w(TAG, "imageReader.close threw", t);
        }
        try {
            if (mediaProjection != null) {
                if (projectionCallback != null) {
                    try {
                        mediaProjection.unregisterCallback(projectionCallback);
                    } catch (Throwable ignore) { }
                }
                mediaProjection.stop();
                mediaProjection = null;
            }
        } catch (Throwable t) {
            Log.w(TAG, "mediaProjection.stop threw", t);
        }
        projectionCallback = null;
        try {
            Intent svcIntent = new Intent(this, MediaProjectionForegroundService.class);
            stopService(svcIntent);
        } catch (Throwable t) {
            Log.w(TAG, "stopService threw", t);
        }
        Log.i(TAG, "stopCaptureInternal complete");
    }
}
