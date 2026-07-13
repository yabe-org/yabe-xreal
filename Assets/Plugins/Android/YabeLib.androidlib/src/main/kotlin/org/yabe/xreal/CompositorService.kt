package org.yabe.xreal

import android.app.Service
import android.content.Intent
import android.os.IBinder
import android.os.ParcelFileDescriptor
import android.util.Log

class CompositorService : Service() {
    companion object {
        private const val TAG = "CompositorService"

        init {
            System.loadLibrary("compositor")
        }
    }

    private val mBinder = object : ICompositorService.Stub() {
        override fun registerSocket(pfd: ParcelFileDescriptor?) {
            if (pfd == null) {
                Log.e(TAG, "Received null ParcelFileDescriptor")
                return
            }
            val fd = pfd.detachFd()
            Log.d(TAG, "Received file descriptor: $fd")
            startCompositor(fd)
        }
    }

    override fun onCreate() {
        super.onCreate()
        Log.d(TAG, "CompositorService onCreate")
    }

    override fun onBind(intent: Intent?): IBinder {
        Log.d(TAG, "CompositorService bound by intent: $intent")
        return mBinder
    }

    override fun onUnbind(intent: Intent?): Boolean {
        Log.d(TAG, "CompositorService unbound")
        return super.onUnbind(intent)
    }

    override fun onDestroy() {
        Log.d(TAG, "CompositorService onDestroy")
        super.onDestroy()
        terminateCompositor();
    }

    private external fun startCompositor(fd: Int)
    private external fun terminateCompositor()
}
