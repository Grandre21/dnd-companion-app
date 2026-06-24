# DA FARE — Monetizzazione (accantonato)

> Punti da affrontare **solo quando si deciderà di aprire la monetizzazione** dell'app.
> Scorporati da [DA-FARE.md](./DA-FARE.md) il 2026-06-24 per non appesantire il backlog operativo.
> Finché l'app resta privata/gratuita tra amici, questi punti restano congelati.
>
> Legenda priorità: 🔴 **bloccante** · 🟠 **alta** · 🟡 **media** · 🟢 **bassa/idea**.

---

## 1. Decisione: modello di monetizzazione

> È il punto **da decidere per primo**: tutto il resto qui sotto dipende da queste scelte.

- 💡 **Modello.** Free vs a pagamento. Cosa sta dietro al paywall (tutta l'app? alcune feature? quante
  campagne?). Acquisto una tantum vs abbonamento. Free tier + premium.
- 💡 **Entitlement.** Come si rappresenta il diritto d'accesso a pagamento (chi ha pagato cosa), dove vive
  (Play Billing, tabella DB, claim sul JWT…).

## 2. Gate di registrazione / ingresso (era §1 Sicurezza)

> 🟠 Originariamente in DA-FARE §1 (Sicurezza), condizionato a "se l'app diventa a pagamento".

- 🟠 **Legare l'accesso all'entitlement d'acquisto.** Se l'app diventa a pagamento, legare l'accesso
  all'**entitlement d'acquisto (Play Billing)** anziché a un codice invito.
- 🟠 **Validazione codici invito server-side.** Se i codici invito restano (es. per il free tier o per
  invitare amici nel gruppo), validarli **server-side** (monouso / con scadenza) anziché solo lato client.

---

## Note / dipendenze

- **Sicurezza già pronta:** l'autorità sui dati è già lato server (RLS su `auth.uid()` +
  `is_campaign_member`/`is_campaign_master`), quindi il gate di monetizzazione si appoggia su fondamenta solide
  — è una questione di *entitlement*, non di riscrivere le autorizzazioni.
- **Feature AI (allowlist) NON è qui:** l'"accesso riservato" dell'aiuto AI (DA-FARE §8) è un *allowlist
  owner+amici* per contenere i costi dell'LLM, **non** un paywall → resta nel DA-FARE normale.
- **Hosting / i18n:** l'hosting alternativo (header di sicurezza) e l'i18n (Play Store globale) restano nel
  DA-FARE normale; toccano il lancio pubblico ma non la monetizzazione in sé.
- **Possibile sinergia:** quando si farà l'edge function/proxy per l'AI (§8), potrebbe ospitare anche la
  verifica dell'entitlement (stesso punto server-side che custodisce segreti e allowlist).
