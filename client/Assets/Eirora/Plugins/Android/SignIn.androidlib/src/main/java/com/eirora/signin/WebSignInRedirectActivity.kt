package com.eirora.signin

import android.app.Activity
import android.os.Bundle

/**
 * Receives the `eirora-signin:` redirect that ends a web sign-in and hands only its address to [WebSignInActivity], the
 * way AppAuth's RedirectUriReceiverActivity does. It is the only exported part of the sign-in: everything else in the
 * intent, such as extras, is ignored, so another app cannot make the sign-in open a page of its choosing. A redirect
 * that another app makes up is rejected by the app, which only accepts the state of its own sign-in.
 */
class WebSignInRedirectActivity : Activity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val redirect = intent?.data
        if (redirect != null && redirect.scheme == WebSignInActivity.SCHEME) {
            startActivity(WebSignInActivity.answer(this, redirect))
        }

        finish()
    }
}
