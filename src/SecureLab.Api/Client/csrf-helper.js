
(() => {
  // CSRF-токен зберігаємо лише в пам'яті поточної сторінки.
  let tokenPromise = null;

  async function getToken() {
    if (!tokenPromise) {
      tokenPromise = (async () => {
        const response = await fetch("/api/auth/csrf-token", {
          credentials: "same-origin",
          cache: "no-store"
        });

        if (!response.ok) {
          throw new Error("Unable to obtain CSRF token.");
        }

        const data = await response.json();

        if (typeof data.token !== "string" || !data.token) {
          throw new Error("Missing CSRF token.");
        }

        return data.token;
      })().catch(error => {
        tokenPromise = null;
        throw error;
      });
    }

    return tokenPromise;
  }

  async function headers() {
    return {
      "X-SecureLab-CSRF": await getToken()
    };
  }

  function reset() {
    tokenPromise = null;
  }

  window.SecureLabCsrf = {
    headers,
    reset
  };
})();
