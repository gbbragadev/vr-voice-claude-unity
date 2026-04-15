/*
 * MediaProjectionForegroundService.java
 * ------------------------------------------------------------
 * VR Voice Claude — Phase 3 Sprint 1 (Task 1.7)
 *
 * Role
 *   Android 14 (API 34) hard-requires that a foreground service of type
 *   FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION be running BEFORE the app
 *   calls MediaProjectionManager.getMediaProjection(resultCode, data).
 *   Skipping this throws SecurityException.
 *
 *   This service exists solely to satisfy that requirement. It:
 *     1. Creates a notification channel on first run (API 26+).
 *     2. Posts a minimal ongoing notification so the user knows capture
 *        is active.
 *     3. Calls startForeground(NOTIFICATION_ID, notification,
 *        FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION).
 *     4. Invokes a static "ready" Runnable that
 *        MediaProjectionCaptureActivity registered before
 *        startForegroundService was called — only then is it safe to
 *        call getMediaProjection().
 *
 *   The activity is responsible for calling stopService(...) during
 *   teardown (see MediaProjectionCaptureActivity.stopCaptureInternal).
 *
 * Unity Gradle
 *   Unity's Android Gradle build picks up every .java file in
 *   Assets/Plugins/Android/ automatically, so this second top-level class
 *   compiles alongside MediaProjectionCaptureActivity.java without any
 *   extra config.
 */
package com.gbbraga.vrvoiceclaude;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.os.Build;
import android.os.IBinder;
import android.util.Log;

import androidx.core.app.NotificationCompat;

public class MediaProjectionForegroundService extends Service {

    private static final String TAG = "VRVoiceClaudeMP";
    private static final String CHANNEL_ID = "vrvoiceclaude-capture";
    private static final int NOTIFICATION_ID = 0xC4B7;

    // Registered by the activity BEFORE startForegroundService, invoked
    // from onStartCommand once startForeground has actually taken effect.
    // Static because Android instantiates services reflectively.
    private static volatile Runnable sReadyCallback;

    public static void setReadyCallback(Runnable r) {
        sReadyCallback = r;
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        try {
            ensureChannel();
            Notification notification = buildNotification();

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                startForeground(
                        NOTIFICATION_ID,
                        notification,
                        ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION);
            } else {
                startForeground(NOTIFICATION_ID, notification);
            }
            Log.i(TAG, "Foreground service started (mediaProjection type)");

            // Signal the activity that it's now safe to call
            // getMediaProjection(resultCode, data).
            Runnable cb = sReadyCallback;
            sReadyCallback = null;
            if (cb != null) {
                try {
                    cb.run();
                } catch (Throwable t) {
                    Log.e(TAG, "ready callback threw", t);
                }
            }
        } catch (Throwable t) {
            Log.e(TAG, "startForeground failed", t);
            stopSelf();
        }
        return START_NOT_STICKY;
    }

    @Override
    public void onDestroy() {
        Log.i(TAG, "Foreground service onDestroy");
        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
                stopForeground(Service.STOP_FOREGROUND_REMOVE);
            } else {
                stopForeground(true);
            }
        } catch (Throwable t) {
            Log.w(TAG, "stopForeground threw", t);
        }
        super.onDestroy();
    }

    private void ensureChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
            return;
        }
        NotificationManager nm =
                (NotificationManager) getSystemService(Context.NOTIFICATION_SERVICE);
        if (nm == null) return;
        if (nm.getNotificationChannel(CHANNEL_ID) != null) {
            return;
        }
        NotificationChannel channel = new NotificationChannel(
                CHANNEL_ID,
                "VR Voice Claude — Captura",
                NotificationManager.IMPORTANCE_LOW);
        channel.setDescription("Captura de tela ativa enquanto o Claude olha o que voce ve.");
        channel.setShowBadge(false);
        nm.createNotificationChannel(channel);
    }

    private Notification buildNotification() {
        NotificationCompat.Builder b = new NotificationCompat.Builder(this, CHANNEL_ID)
                .setContentTitle("VR Voice Claude esta capturando o que voce ve")
                .setContentText("Toque pra parar")
                .setSmallIcon(android.R.drawable.ic_menu_camera)
                .setOngoing(true)
                .setPriority(NotificationCompat.PRIORITY_LOW)
                .setCategory(NotificationCompat.CATEGORY_SERVICE);
        return b.build();
    }
}
