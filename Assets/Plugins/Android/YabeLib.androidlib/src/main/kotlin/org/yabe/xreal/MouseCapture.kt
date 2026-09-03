package org.yabe.xreal

import android.app.Activity
import android.util.Log
import android.view.View
import android.view.ViewTreeObserver
import java.lang.ref.WeakReference

object MouseCapture {
    private const val TAG = "YabeXreal"

    @Volatile
    private var captureRequested = false

    private var currentView: WeakReference<View>? = null

    private val focusListener = ViewTreeObserver.OnWindowFocusChangeListener { hasFocus ->
        Log.i(TAG, "Mouse capture window focus: $hasFocus")
        val view = currentView?.get()
        if (!hasFocus || !captureRequested || view == null) {
            return@OnWindowFocusChangeListener
        }
        applyMouseCapture(view)
    }

    @JvmStatic
    fun setMouseCapture(activity: Activity, shouldCapture: Boolean) {
        captureRequested = shouldCapture
        activity.runOnUiThread {
            val view = activity.window.decorView
            bind(view)
            applyMouseCapture(view)
        }
    }

    private fun bind(view: View) {
        val oldView = currentView?.get()
        if (oldView === view) {
            return
        }
        val oldObserver = oldView?.viewTreeObserver
        if (oldObserver?.isAlive == true) {
            oldObserver.removeOnWindowFocusChangeListener(focusListener)
        }
        currentView = WeakReference(view)
        view.viewTreeObserver.addOnWindowFocusChangeListener(focusListener)
    }

    private fun applyMouseCapture(view: View) {
        Log.i(
            TAG,
            "Mouse capture view state: attached=${view.isAttachedToWindow}, focused=${view.hasWindowFocus()}",
        )
        if (!captureRequested) {
            view.releasePointerCapture()
            return
        }
        if (!view.hasWindowFocus()) {
            Log.i(TAG, "Mouse capture is pending until window focus is acquired")
            return
        }
        view.requestPointerCapture()
    }
}
