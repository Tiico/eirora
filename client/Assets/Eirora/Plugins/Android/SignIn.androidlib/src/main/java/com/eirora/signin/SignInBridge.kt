package com.eirora.signin

import android.app.Activity
import android.content.Intent
import androidx.credentials.CredentialManager
import androidx.credentials.CustomCredential
import androidx.credentials.GetCredentialRequest
import androidx.credentials.exceptions.GetCredentialCancellationException
import androidx.credentials.exceptions.GetCredentialException
import androidx.credentials.exceptions.NoCredentialException
import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential
import com.google.android.libraries.identity.googleid.GoogleIdTokenParsingException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch

/**
 * What the game asks of Apple and Google on Android (ADR-0008). Each call answers exactly once through its
 * [SignInCallback]. Nothing here logs, stores or sends a token; the game hands it to Eirora's server.
 */
object SignInBridge {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)

    @Volatile
    private var pendingWebSignIn: SignInCallback? = null

    /**
     * The "Sign in with Google" sheet through Credential Manager. The ID token is issued to [webClientId] and carries
     * [nonceHash] as its nonce. Only the ID token is used; Eirora's server keeps neither name nor email.
     */
    @JvmStatic
    fun signInWithGoogle(activity: Activity, webClientId: String, nonceHash: String, callback: SignInCallback) {
        val option = GetSignInWithGoogleOption.Builder(webClientId).setNonce(nonceHash).build()
        val request = GetCredentialRequest.Builder().addCredentialOption(option).build()
        scope.launch {
            try {
                val credential = CredentialManager.create(activity).getCredential(activity, request).credential
                if (credential is CustomCredential) {
                    callback.onSuccess(GoogleIdTokenCredential.createFrom(credential.data).idToken)
                } else {
                    callback.onError("unexpected credential")
                }
            } catch (e: GetCredentialCancellationException) {
                callback.onCancel()
            } catch (e: NoCredentialException) {
                callback.onError("no Google account")
            } catch (e: GetCredentialException) {
                callback.onError(e.type)
            } catch (e: GoogleIdTokenParsingException) {
                callback.onError("unreadable credential")
            }
        }
    }

    /**
     * Opens [url] in a Custom Tab and answers with the `eirora-signin:` URL the sign-in ends on, or with
     * [SignInCallback.onCancel] when the player closes the page. A new sign-in cancels one that is still open.
     */
    @JvmStatic
    fun authenticate(activity: Activity, url: String, callback: SignInCallback) {
        pendingWebSignIn?.onCancel()
        pendingWebSignIn = callback
        try {
            activity.startActivity(Intent(activity, WebSignInActivity::class.java).putExtra(WebSignInActivity.EXTRA_URL, url))
        } catch (error: RuntimeException) {
            // The caller gets the exception and lets the callback go, so nothing may answer it later.
            pendingWebSignIn = null
            throw error
        }
    }

    internal fun completeWebSignIn(answer: String?, error: String? = null) {
        val callback = pendingWebSignIn ?: return
        pendingWebSignIn = null
        when {
            error != null -> callback.onError(error)
            answer != null -> callback.onSuccess(answer)
            else -> callback.onCancel()
        }
    }
}
