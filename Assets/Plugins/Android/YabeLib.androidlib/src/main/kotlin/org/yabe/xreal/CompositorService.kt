package org.yabe.xreal

import android.app.Service
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.ServiceConnection
import android.os.IBinder
import android.os.ParcelFileDescriptor
import android.util.Log

class CompositorService : Service() {
    companion object {
        private const val TAG = "YabeXreal"
        private const val CLIENT_PACKAGE = "org.yabe.xreal_client"
        private const val CLIENT_SERVICE = "$CLIENT_PACKAGE.CompositorService"

        init {
            System.loadLibrary("compositor")
        }
    }

    @Volatile
    private var clientService: ICompositorService? = null
    private var isBoundToClientService = false
    private var hasNotifiedCompositorReady = false
    private var isShuttingDown = false
    private var pendingConfigText: String? = null
    private val binder = object : ICompositorService.Stub() {
        override fun isCompositorReady() = isCompositorRunning()

        override fun setConfigText(configText: String?) {
            if (configText == null) {
                Log.e(TAG, "Received null config contents")
                return
            }
            pendingConfigText = configText
            startAndNotifyIfPossible()
        }

        override fun addClientFd(pfd: ParcelFileDescriptor?) {
            if (!isCompositorRunning()) {
                Log.e(TAG, "Called addClientFd() but compositor is not running")
                return
            }
            if (pfd == null) {
                Log.e(TAG, "Received null ParcelFileDescriptor")
                return
            }
            val fd = pfd.detachFd()
            Log.d(TAG, "Received file descriptor: $fd")
            addClientFd(fd)
        }

        override fun notifyCompositorReady() {
            Log.e(TAG, "Unexpected call from client: notifyCompositorReady()")
        }

        override fun createKeymap(
            rules: String?,
            model: String?,
            layout: String?,
            variant: String?,
            options: String?
        ): String? {
            Log.e(TAG, "Unexpected call from client: createKeymap()")
            return null
        }

        override fun exec(argv: Array<out String>?): Int {
            Log.e(TAG, "Unexpected call from client: exec()")
            return -1
        }

    }

    private val serviceConnection = object : ServiceConnection {
        override fun onServiceConnected(name: ComponentName?, service: IBinder?) {
            Log.d(TAG, "CompositorService connected")
            clientService = ICompositorService.Stub.asInterface(service)
            startAndNotifyIfPossible()
        }

        override fun onServiceDisconnected(name: ComponentName?) {
            Log.d(TAG, "CompositorService disconnected")
            clientService = null
            isBoundToClientService = false
            hasNotifiedCompositorReady = false
        }
    }

    private fun bindToCompositorService() {
        val intent = Intent().setComponent(ComponentName(CLIENT_PACKAGE, CLIENT_SERVICE))
        val success = bindService(intent, serviceConnection, Context.BIND_AUTO_CREATE)
        isBoundToClientService = success
        Log.d(TAG, "bindService() ${if (success) "succeeded" else "failed"}")
    }

    override fun onCreate() {
        super.onCreate()
        Log.d(TAG, "CompositorService onCreate")
        bindToCompositorService()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        // The compositor must not be recreated after this task and its process are gone
        return START_NOT_STICKY
    }

    override fun onBind(intent: Intent?): IBinder {
        Log.d(TAG, "CompositorService bound by intent: $intent")
        return binder
    }

    override fun onUnbind(intent: Intent?): Boolean {
        Log.d(TAG, "CompositorService unbound")
        return super.onUnbind(intent)
    }

    override fun onTaskRemoved(rootIntent: Intent?) {
        Log.d(TAG, "CompositorService task removed")
        shutdown()
        stopSelf()
        super.onTaskRemoved(rootIntent)
    }

    override fun onDestroy() {
        Log.d(TAG, "CompositorService onDestroy")
        shutdown()
        super.onDestroy()
    }

    private fun shutdown() {
        if (isShuttingDown) {
            return
        }
        isShuttingDown = true
        if (isBoundToClientService) {
            unbindService(serviceConnection)
            isBoundToClientService = false
        }
        clientService = null
        terminateCompositor()
    }

    private fun startAndNotifyIfPossible() {
        val service = clientService ?: return
        val configText = pendingConfigText ?: return
        if (!isCompositorRunning()) {
            startCompositor(configText)
        }
        if (hasNotifiedCompositorReady) {
            return
        }
        try {
            service.notifyCompositorReady()
            hasNotifiedCompositorReady = true
        } catch (e: Exception) {
            Log.e(TAG, "Failed to notify that the compositor is ready", e)
        }
    }

    private fun createKeymap(
        rules: String,
        model: String,
        layout: String,
        variant: String,
        options: String
    ): String? {
        val service = clientService ?: return null
        return try {
            service.createKeymap(rules, model, layout, variant, options)
        } catch (e: Exception) {
            Log.e(TAG, "Failed to create keymap", e)
            null
        }
    }

    private fun exec(argv: Array<String>): Int {
        val service = clientService ?: return -1
        return try {
            service.exec(argv)
        } catch (e: Exception) {
            Log.e(TAG, "Failed to execute command", e)
            -1
        }
    }

    private external fun startCompositor(configText: String)
    private external fun terminateCompositor()
    private external fun addClientFd(fd: Int)
    private external fun isCompositorRunning(): Boolean
}
