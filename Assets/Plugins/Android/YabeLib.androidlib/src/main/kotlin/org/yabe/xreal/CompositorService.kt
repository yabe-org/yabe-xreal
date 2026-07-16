package org.yabe.xreal

import android.app.Service
import android.content.ComponentName
import android.content.Intent
import android.content.ServiceConnection
import android.os.IBinder
import android.os.ParcelFileDescriptor
import android.util.Log
import org.yabe.xreal.ICompositorService

class CompositorService : Service() {
    companion object {
        private const val TAG = "YabeXreal"

        init {
            System.loadLibrary("compositor")
        }
    }

    private var compositorService: ICompositorService? = null
    private var isBoundToClientService = false
    private var hasNotifiedCompositorReady = false
    private val mBinder = object : ICompositorService.Stub() {
        override fun isCompositorReady(): Boolean {
            return isCompositorRunning()
        }

        override fun notifyCompositorReady() {
            Log.e(TAG, "Unexpected call from client: notifyCompositorReady()")
        }

        override fun addClientFd(pfd: ParcelFileDescriptor?) {
            if (pfd == null) {
                Log.e(TAG, "Received null ParcelFileDescriptor")
                return
            }
            val fd = pfd.detachFd()
            Log.d(TAG, "Received file descriptor: $fd")
            addClientFd(fd)
        }
    }
    private val serviceConnection = object : ServiceConnection {
        override fun onServiceConnected(name: ComponentName?, service: IBinder?) {
            Log.d(TAG, "CompositorService connected")
            compositorService = ICompositorService.Stub.asInterface(service)
            notifyClientIfReady()
        }

        override fun onServiceDisconnected(name: ComponentName?) {
            Log.d(TAG, "CompositorService disconnected")
            compositorService = null
            isBoundToClientService = false
            hasNotifiedCompositorReady = false
        }
    }

    private fun bindToCompositorService() {
        val intent = Intent()
        intent.component = ComponentName("org.yabe.xreal_client", "org.yabe.xreal_client.CompositorService")
        val success = bindService(intent, serviceConnection, 0)
        isBoundToClientService = success
        if (success) {
            Log.d(TAG, "bindService() succeeded")
        } else {
            Log.d(TAG, "bindService() failed")
        }
    }

    override fun onCreate() {
        super.onCreate()
        Log.d(TAG, "CompositorService onCreate")
        startCompositorIfNeeded()
        bindToCompositorService()
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
        if (isBoundToClientService) {
            unbindService(serviceConnection)
            isBoundToClientService = false
        }
        super.onDestroy()
        terminateCompositor();
    }

    private fun startCompositorIfNeeded() {
        if (isCompositorRunning()) {
            return
        }
        startCompositor()
    }

    private fun notifyClientIfReady() {
        if (compositorService == null || hasNotifiedCompositorReady || !isCompositorRunning()) {
            return
        }
        try {
            compositorService?.notifyCompositorReady()
            hasNotifiedCompositorReady = true
        } catch (e: Exception) {
            Log.e(TAG, "Failed to notify that the compositor is ready", e)
        }
    }

    private external fun startCompositor()
    private external fun terminateCompositor()
    private external fun addClientFd(fd: Int)
    private external fun isCompositorRunning(): Boolean
}
