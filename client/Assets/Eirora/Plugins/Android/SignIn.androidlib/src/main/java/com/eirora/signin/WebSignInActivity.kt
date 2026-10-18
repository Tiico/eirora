package com.eirora.signin

import android.app.Activity
import android.content.ActivityNotFoundException
import android.content.Intent
import android.net.Uri
import android.os.Bundle
import androidx.browser.customtabs.CustomTabsIntent

/**
 * Hosts a web sign-in: opens the page in a Custom Tab and receives the `eirora-signin:` redirect that ends it, the way
 * AppAuth's authorization activity does. Coming back without a redirect means the player closed the page.
 */
class WebSignInActivity : Activity() {
    private var started = false

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        // Started by the redirect itself, when the game was closed while the page was open: only answer.
        started = savedInstanceState?.getBoolean(KEY_STARTED) ?: (intent?.data != null)
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
    }

    override fun onResume() {
        super.onResume()
        if (!started) {
            started = true
            if (!open(intent?.getStringExtra(EXTRA_URL))) {
                SignInBridge.completeWebSignIn(null, "no browser")
                finish()
            }
            return
        }

        SignInBridge.completeWebSignIn(intent?.data?.toString())
        finish()
    }

    override fun onSaveInstanceState(outState: Bundle) {
        super.onSaveInstanceState(outState)
        outState.putBoolean(KEY_STARTED, started)
    }

    private fun open(url: String?): Boolean {
        val uri = Uri.parse(url ?: return false)
        return try {
            CustomTabsIntent.Builder().build().launchUrl(this, uri)
            true
        } catch (e: ActivityNotFoundException) {
            try {
                startActivity(Intent(Intent.ACTION_VIEW, uri))
                true
            } catch (e: ActivityNotFoundException) {
                false
            }
        }
    }

    companion object {
        const val EXTRA_URL = "eirora.url"
        private const val KEY_STARTED = "eirora.started"
    }
}
