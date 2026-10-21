// The deletion page (DeletionPage.cs): a fresh sign-in with Apple or Google, bound to a nonce like the app's
// sign-ins, confirms that the account is the player's; the server then deletes it. Nothing is stored in the browser.
"use strict";

(function () {
  const config = document.body.dataset;
  let confirmation = null;

  const show = (id, visible) => { document.getElementById(id).hidden = !visible; };
  const say = (text) => { document.getElementById("message").textContent = text; };

  function randomBase64Url(bytes) {
    const buffer = crypto.getRandomValues(new Uint8Array(bytes));
    return btoa(String.fromCharCode(...buffer)).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
  }

  // A new nonce and its SHA-256 as lowercase hex, which the provider puts in the ID token.
  async function newNonce() {
    const nonce = randomBase64Url(32);
    const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(nonce));
    const hash = Array.from(new Uint8Array(digest), (b) => b.toString(16).padStart(2, "0")).join("");
    return { nonce, hash };
  }

  function signedIn(provider, idToken, nonce, authorizationCode) {
    confirmation = { provider, idToken, nonce, authorizationCode };
    document.getElementById("signed-in").textContent =
      `Du är inloggad med ${provider === "apple" ? "Apple" : "Google"}. Vill du radera kontot för gott? Det går inte att ångra.`;
    say("");
    show("sign-in", false);
    show("confirm", true);
  }

  function restart() {
    confirmation = null;
    show("confirm", false);
    show("sign-in", true);
    prepareApple();
    prepareGoogle();
  }

  let appleState = null;
  let appleNonce = null;

  async function prepareApple() {
    if (!config.appleServicesId || !window.AppleID) {
      return;
    }

    const { nonce, hash } = await newNonce();
    appleNonce = nonce;
    appleState = randomBase64Url(16);
    window.AppleID.auth.init({
      clientId: config.appleServicesId,
      redirectURI: `${location.origin}/account/delete`,
      state: appleState,
      nonce: hash,
      usePopup: true,
    });
    show("apple", true);
  }

  document.addEventListener("AppleIDSignInOnSuccess", (event) => {
    const authorization = event.detail.authorization;
    if (authorization.state !== appleState) {
      say("Inloggningen hörde till ett annat försök. Försök igen.");
      restart();
      return;
    }

    signedIn("apple", authorization.id_token, appleNonce, authorization.code);
  });

  document.addEventListener("AppleIDSignInOnFailure", (event) => {
    if (event.detail && event.detail.error !== "popup_closed_by_user" && event.detail.error !== "user_cancelled_authorize") {
      say("Inloggningen med Apple gick inte. Försök igen.");
    }

    prepareApple();
  });

  async function prepareGoogle() {
    if (!config.googleClientId || !window.google) {
      return;
    }

    const { nonce, hash } = await newNonce();
    window.google.accounts.id.initialize({
      client_id: config.googleClientId,
      nonce: hash,
      auto_select: false,
      callback: (response) => signedIn("google", response.credential, nonce, null),
    });
    const button = document.getElementById("google");
    button.replaceChildren();
    window.google.accounts.id.renderButton(button, {
      type: "standard",
      theme: "outline",
      size: "large",
      text: "signin_with",
      shape: "pill",
      width: 260,
      locale: "sv",
    });
    show("google", true);
  }

  document.getElementById("cancel").addEventListener("click", restart);

  document.getElementById("delete").addEventListener("click", async () => {
    if (!confirmation) {
      return;
    }

    document.getElementById("delete").disabled = true;
    say("Raderar …");
    let status = 0;
    try {
      const response = await fetch("/account/delete", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(confirmation),
      });
      status = response.status;
    } catch {
      status = 0;
    }

    document.getElementById("delete").disabled = false;
    if (status === 204) {
      show("confirm", false);
      say("Kontot är raderat. Tack för att du spelade Eirora.");
      return;
    }

    say({
      403: "Kontot har en Apple-inloggning. Bekräfta med Apple i stället.",
      404: "Det finns inget konto för den här inloggningen. Det kan redan vara raderat.",
      400: "Inloggningen kunde inte bekräftas. Försök igen.",
    }[status] || "Det gick inte just nu. Försök igen om en stund.");
    restart();
  });

  window.addEventListener("load", async () => {
    await Promise.all([prepareApple(), prepareGoogle()]);
    if (document.getElementById("apple").hidden && document.getElementById("google").hidden) {
      show("unavailable", true);
    }
  });
})();
