# Consumer Information Page — Draft Content

> **File purpose:** Draft content for the mandatory Akahu Consumer Information Page.
> Build this as a publicly accessible Next.js page at `frontend/src/app/open-banking/page.tsx`
> (or a URL confirmed with Akahu at submission).
>
> Design note: The page should be clean and readable — same visual style as `/privacy`.
> Replace all `[INSERT AKAHU LOGO HERE]` markers with the official Akahu logo asset
> obtained from Akahu before launch. Akahu branding guidelines apply.
>
> Audience: New Zealand users connecting their bank accounts to MyMascada.
> Tone: Plain English. No legalese. Approx. 450–550 words of body content.

---

## Page title

**How MyMascada connects to your bank**

_Powered by_  [INSERT AKAHU LOGO HERE]

---

## Body content

### What is bank connection?

When you connect a bank account to MyMascada, the app reads your account information and transaction history automatically — so you don't have to import CSV files or enter transactions by hand. Your spending is categorised, your budgets stay up to date, and your financial picture stays current.

This connection is read-only. MyMascada can never move money, make payments, or change anything at your bank. It can only see what's there.

---

### Who provides the connection?

MyMascada uses **Akahu** to connect to New Zealand banks and financial institutions.

[INSERT AKAHU LOGO HERE]

Akahu is a New Zealand open-finance platform that facilitates secure, consent-based access to financial data. When you connect your bank, you are granting consent directly through Akahu's secure authorisation flow — not entering your bank credentials into MyMascada.

Akahu's role is to act as the intermediary between your bank and MyMascada. They hold your authorisation on your behalf and can relay account and transaction data to MyMascada only while you have an active, consented connection.

For more about how Akahu handles your data, see the [Akahu Privacy Policy](https://www.akahu.nz/privacy).

---

### What data does MyMascada access?

When you connect a bank account, MyMascada requests access to:

- **Account information** — account name, type, and current balance.
- **Transaction history** — up to 365 days of past transactions at the time of connection, and new transactions as they occur while the connection is active.

MyMascada does **not** request access to payments, transfers, account management, or any other bank functionality. Access is strictly read-only.

---

### How long does the access last?

Your connection remains active until you choose to end it. This is called an enduring consent — it does not expire automatically.

You are in control. You can disconnect a bank account at any time from **Settings → Bank Connections** inside MyMascada. When you disconnect, your authorisation is immediately revoked through Akahu, and no further data is retrieved from that account.

If you delete your MyMascada account, all bank connections are disconnected and all stored data is permanently deleted.

---

### What happens to your data?

Transaction and account data synced through Akahu is stored securely in MyMascada's database and is used solely to power the features you see in the app — your dashboard, transaction list, budgets, and spending insights.

MyMascada does not sell your financial data. It is not shared with third parties except as described in the [Privacy Policy](/privacy).

---

### How do I connect my bank?

1. Go to **Settings → Bank Connections** in MyMascada.
2. Click **Connect bank account**.
3. You will be taken to Akahu's secure authorisation page, where you log in to your bank and approve access.
4. Once authorised, your accounts and recent transactions will appear in MyMascada automatically.

---

### Questions?

If you have questions about how MyMascada uses your bank data, contact us at [support@mymascada.com](mailto:support@mymascada.com).

For questions about Akahu's role in this process, visit [akahu.nz](https://www.akahu.nz) or review the [Akahu Privacy Policy](https://www.akahu.nz/privacy).

[INSERT AKAHU LOGO HERE]

---

## Implementation notes (for developer — remove before launch)

- Place the page at `frontend/src/app/open-banking/page.tsx` (no auth required — `allowAnonymous`).
- The page should be linked from:
  - The bank connections settings page (`/settings/bank-connections`) — add a "Learn how this works" link near the info banner.
  - The privacy policy page (`/privacy`) — add a sentence in the Akahu section linking here.
  - Akahu's consent screen will display the URL as part of the application review — confirm the exact URL with Akahu before submission.
- Replace `[INSERT AKAHU LOGO HERE]` with `<Image src="/akahu-logo.svg" alt="Akahu" ... />`. Obtain the official logo SVG from Akahu.
- Add the page URL to the Akahu Developer Console under "Consumer Information Page URL" when submitting the accreditation application.
- Localise the page content via `frontend/messages/en.json` and `frontend/messages/pt-BR.json` if Brazilian Portuguese support is required (PT-BR users are unlikely to be NZ bank users, but maintain consistency with the i18n setup).
