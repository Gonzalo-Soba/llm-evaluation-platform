# Labeling guidelines: support messages

These guidelines define how to label the customer support message dataset. Messages are in Spanish. Each message gets exactly one category and one difficulty level.

The guiding principle is: **label by the primary action the support team must take**, not by the topic or the words in the message.

## Categories

### technical_support

**Definition:** The product does not work as expected and the team must investigate or fix a malfunction.

- **Includes:** errors, crashes, bugs, pages that do not load, features that behave incorrectly, performance problems, sync or integration failures.
- **Excludes:** questions about how to use a feature that works correctly (`general_inquiry`). Login problems caused by credentials or account state (`account_management`). Incorrect charges, even if a bug caused them (`billing`, see the boundary rules).

**Examples:**
- "La app se cierra cada vez que intento subir una foto."
- "Desde la última actualización el reporte mensual sale en blanco."

### billing

**Definition:** The request concerns money: charges, payments, invoices, refunds, or the subscription plan the customer pays for.

- **Includes:** duplicate or unexpected charges, failed payments, refund requests, invoices (missing, incorrect, requesting a copy), payment method changes, plan upgrades and downgrades, subscription cancellation.
- **Excludes:** general pricing questions from someone who is not asking about their own charges or plan (`general_inquiry`). Deleting the account itself (`account_management`). Errors on billing pages where no money issue is reported (`technical_support`).

**Examples:**
- "Me cobraron dos veces la suscripción de este mes."
- "Necesito la factura de marzo con los datos de mi empresa."

### account_management

**Definition:** The request concerns access to the account or its data and settings, and the team must act on the account itself.

- **Includes:** password resets, login problems due to credentials, locked or suspended accounts (for non-payment reasons), changing email or username, two-factor authentication, managing users or permissions, closing or deleting the account, personal data requests.
- **Excludes:** login screens that crash or show a system error (`technical_support`). Access blocked because a payment failed (`billing`). Plan changes and subscription cancellation (`billing`).

**Examples:**
- "Olvidé mi contraseña y no me llega el correo para restablecerla."
- "Quiero cambiar el email asociado a mi cuenta."

### general_inquiry

**Definition:** The customer asks for information and no fix, charge or account change is needed.

- **Includes:** how a feature works, what the product can do, pricing and plans when asked as general information, opening hours, contact channels, policies, feedback and suggestions.
- **Excludes:** reports that something works incorrectly (`technical_support`). Questions about the customer's own charges or plan (`billing`). Requests that require changing the account (`account_management`).

**Examples:**
- "¿Cómo puedo exportar mis datos a Excel?"
- "¿Tienen algún descuento para organizaciones sin fines de lucro?"

## Boundary rules

Apply these rules when a message could fit more than one category. In every case, the question is: **what is the main thing the support team must do to resolve this message?**

### 1. Login failure caused by a failed payment → `billing`

If the customer cannot log in or their account is blocked *because* a payment failed or the subscription expired, label it `billing`. Access is restored by resolving the payment, not by changing the account.

- "No puedo entrar, dice que mi cuenta está suspendida por un pago rechazado." → `billing`
- "No puedo entrar, dice que mi contraseña es incorrecta." → `account_management`
- "Al iniciar sesión aparece un error 500." → `technical_support`

### 2. Plan change or subscription cancellation → `billing`

Upgrading, downgrading or cancelling a subscription changes what the customer is charged, so it is `billing`. Closing or deleting the account and its data is `account_management`.

- "Quiero pasar del plan Pro al plan Básico." → `billing`
- "Quiero cancelar mi suscripción a partir del mes que viene." → `billing`
- "Quiero eliminar mi cuenta y todos mis datos." → `account_management`

### 3. Bug inside the billing page → `technical_support`, unless there is a money issue

A malfunction on a billing-related page is `technical_support` when the action is fixing the page. If the message reports a wrong charge, a missing refund or a payment that went through incorrectly, the action is to fix the money issue, so it is `billing`.

- "La página de facturación no carga, se queda en blanco." → `technical_support`
- "El botón de descargar factura da error." → `technical_support`
- "El botón de pagar falló y me cobraron dos veces." → `billing`

### 4. How a feature works: `general_inquiry` vs `technical_support`

If the customer wants to know how to do something and nothing indicates a malfunction, label it `general_inquiry`. If they describe an expected behavior that does not happen (an error, missing results, something that "no funciona"), label it `technical_support`.

- "¿Cómo programo un reporte para que se envíe cada semana?" → `general_inquiry`
- "Programé un reporte semanal pero nunca me llega." → `technical_support`

When it is unclear whether the customer is doing something wrong or the feature is broken, prefer `technical_support` if the message describes a concrete failed attempt, and `general_inquiry` if it only asks how to do it.

### 5. Messages with several requests

Choose one primary request using these rules, in order:

1. A request that requires an action (fix, charge, account change) takes precedence over a purely informational question.
2. If several requests require action, choose the one that blocks the customer from using the product or that they present as most urgent.
3. If there is still a tie, choose the first request mentioned.

- "No puedo entrar a mi cuenta. Por cierto, ¿tienen app para tablet?" → `account_management` (action over information)
- "Me cobraron de más y además la app va un poco lenta." → `billing` (the charge is presented as the main problem)

Messages labeled with this rule are usually `ambiguous`.

### Other cases

- **Greetings, thanks and emotional tone** do not affect the category. Label the underlying request.
- **General pricing vs. own charges:** "¿Cuánto cuesta el plan Pro?" is `general_inquiry`. "¿Por qué me cobraron el precio del plan Pro?" is `billing`.

## Difficulty

Each message also gets a difficulty level:

- **`clear`:** the message has one obvious category. Any reader following these guidelines would choose it without hesitation.
- **`ambiguous`:** a reasonable person could hesitate between two or more categories, and the label is resolved by applying one of the boundary rules above.

If you label a message `ambiguous`, you must be able to name the boundary rule that decides its category. If no rule covers it, flag the message for review instead of guessing, and consider adding a new rule to these guidelines.
