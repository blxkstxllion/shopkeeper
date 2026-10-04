import { Link } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'
import { Logo } from '@/components/ui/Logo'
import { Alert } from '@/components/ui/Alert'

const LAST_UPDATED = 'October 4, 2026'

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="flex flex-col gap-2">
      <h2 className="text-base font-semibold text-slate-900 dark:text-slate-100">{title}</h2>
      <div className="flex flex-col gap-3 text-sm leading-relaxed text-slate-600 dark:text-slate-300">{children}</div>
    </section>
  )
}

/**
 * Starter draft, not final legal copy - written from an inventory of what the app actually does
 * (see the commands/handlers cited inline) rather than generic boilerplate, but it still needs a
 * real legal review (and the bracketed placeholders filled in) before this is the policy of
 * record. Public route, reachable without signing in - see AppRouter.
 */
export function PrivacyPolicyPage() {
  return (
    <div className="min-h-screen bg-slate-50 px-4 py-12 dark:bg-slate-950">
      <div className="mx-auto max-w-3xl">
        <div className="mb-8 flex flex-col items-center gap-2 text-center">
          <Logo className="h-11 w-11" />
          <p className="text-lg font-semibold text-slate-900 dark:text-slate-100">The Shop Keeper</p>
        </div>

        <div className="mb-6">
          <Alert tone="info">
            <strong>Draft for review.</strong> This page was written from an honest inventory of what the app collects
            and does today - it is a starting point, not final legal advice. Have a lawyer review it (especially the
            data-retention, sub-processor, and international-transfer sections) and fill in the bracketed placeholders
            before relying on it.
          </Alert>
        </div>

        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-700 dark:bg-slate-900 sm:p-10">
          <h1 className="mb-1 text-2xl font-bold text-slate-900 dark:text-slate-100">Privacy Policy</h1>
          <p className="mb-8 text-sm text-slate-400">Last updated: {LAST_UPDATED}</p>

          <div className="flex flex-col gap-8">
            <Section title="1. Who this policy covers">
              <p>
                The Shop Keeper ("the Service") is operated by Omni Fix Labs ("we," "us," "our"). This policy describes
                how we handle personal data when a business ("you," "your business") signs up and uses the Service, and
                when that business's own employees and customers interact with data stored in it.
              </p>
              <p>There are two different roles we play, and they come with different responsibilities:</p>
              <ul className="list-disc space-y-1 pl-5">
                <li>
                  For your own account data (name, email, login activity) and your business's account on the Service, we
                  are the <strong>data controller</strong> - we decide why that data is collected.
                </li>
                <li>
                  For data you enter about your own customers and suppliers (names, phone numbers, addresses,
                  purchase/credit history), we are a <strong>data processor</strong> acting on your instructions - you
                  are the controller of that data, and you're responsible for having a lawful basis to collect it from
                  the people you serve.
                </li>
              </ul>
            </Section>

            <Section title="2. Information we collect">
              <p>
                <strong>Account information.</strong> When you or a teammate is invited to a business, we collect name,
                email address, and a password. Passwords are never stored in plain text - they're hashed with bcrypt,
                and we can't read them back.
              </p>
              <p>
                <strong>Business operational data.</strong> Everything you use the Service to run your business with:
                products and inventory, sales and refunds, expenses, suppliers, branches, and employee roles. Financial
                records (sales, refunds) are kept as permanent, append-only history, consistent with standard
                bookkeeping practice - they are corrected with a reversing entry, not edited or deleted.
              </p>
              <p>
                <strong>Your customers' data.</strong> If you choose to track customers (for receipts, credit accounts,
                or purchase history), we store what you enter: name, and optionally phone, email, and address. We never
                collect this directly from your customers ourselves - you do, and you're responsible for telling them
                how it's used.
              </p>
              <p>
                <strong>Uploaded images.</strong> Product photos, your profile photo, and your business logo, if you
                choose to upload any.
              </p>
              <p>
                <strong>Payment information.</strong> If your business subscribes to a paid plan, checkout is handled
                entirely by our payment processor, Paystack - we never see or store your card number. We keep a record
                that a subscription exists and its status (active, cancelled, renewal date).
              </p>
              <p>
                <strong>Usage and device data.</strong> Login timestamps, IP address (used for login rate-limiting and
                audit trails), and an activity log of who changed what and when within your business account. Sensitive
                fields (like a customer's name or address) are redacted to <code>[REDACTED]</code> inside that activity
                log rather than kept there in plain text a second time.
              </p>
            </Section>

            <Section title="3. How we use this information">
              <ul className="list-disc space-y-1 pl-5">
                <li>To operate the Service: process sales, track inventory, generate reports, and so on.</li>
                <li>To authenticate you and keep your business's data separate from every other business's.</li>
                <li>To send account-related email (email verification, password resets, invitations).</li>
                <li>To detect and prevent abuse (login rate-limiting, refresh-token theft detection).</li>
                <li>
                  To power the optional AI Advisor feature, if your plan includes it - see the next section for exactly
                  what that does and doesn't send anywhere.
                </li>
              </ul>
              <p>We do not sell personal data, and we do not use it for advertising.</p>
            </Section>

            <Section title="4. The AI Advisor feature">
              <p>
                On plans that include it, the AI Advisor answers questions like "what's my best-selling product" or
                "how's my profit margin this month." It works in a deliberately narrow way: the Service computes the
                real answer from your own data first, and only that already-computed answer (e.g. "Product X, GHS 4,200
                in revenue") - never your raw customer list, sales ledger, or any other underlying record - is sent to
                our AI provider, Anthropic, so it can be phrased in natural language. Anthropic is instructed to repeat
                every number and name exactly as given, not to alter or add to them.
              </p>
              <p>This feature is entirely optional and only active on plans that enable it.</p>
            </Section>

            <Section title="5. Who we share information with">
              <p>We share information with a small number of service providers who help us run the Service:</p>
              <ul className="list-disc space-y-1 pl-5">
                <li>
                  <strong>Paystack</strong> - processes subscription payments. They receive what's needed to complete a
                  payment; we don't see your card details.
                </li>
                <li>
                  <strong>Our email provider</strong> (AWS SES or Resend, depending on configuration) - sends
                  transactional email (verification, password reset, invitations) on our behalf.
                </li>
                <li>
                  <strong>Anthropic</strong> - powers the optional AI Advisor feature described above, only for
                  businesses on a plan that includes it.
                </li>
                <li>
                  <strong>Amazon Web Services</strong> - hosts encrypted nightly database backups.
                </li>
              </ul>
              <p>
                We do not sell personal data to third parties, and we do not share it for their own marketing purposes.
              </p>
            </Section>

            <Section title="6. How we protect information">
              <ul className="list-disc space-y-1 pl-5">
                <li>Passwords are hashed with bcrypt and never stored or logged in plain text.</li>
                <li>
                  Sessions use a short-lived access token kept in memory only (never written to browser storage) plus a
                  long-lived refresh token stored as an HTTP-only cookie your browser's JavaScript can't read. Refresh
                  tokens are rotated on every use, and reuse of a stolen token is detected and revokes the whole session
                  chain.
                </li>
                <li>All traffic to the Service is encrypted in transit (TLS).</li>
                <li>
                  Every business's data is isolated at the database layer - a bug in one screen cannot accidentally show
                  one business another business's records.
                </li>
                <li>
                  A point-of-sale terminal left unattended automatically signs out after 15 minutes of inactivity.
                </li>
                <li>Database backups run nightly and are encrypted at rest.</li>
              </ul>
            </Section>

            <Section title="7. How long we keep information">
              <p>
                We keep business operational data (sales, inventory, customer records) for as long as your business's
                account is active, since it's the historical record the Service is built to maintain. Activity/audit
                logs are kept indefinitely for accountability, with sensitive fields redacted as described above. If you
                close your account,{' '}
                <span className="font-medium">
                  [placeholder: describe your actual deletion timeline here, e.g. "we delete your data within 90 days,
                  except where backups require more time to age out"]
                </span>
                .
              </p>
            </Section>

            <Section title="8. Your rights">
              <p>
                Depending on where you're located, you may have the right to access, correct, export, or request
                deletion of your personal data. To exercise any of these, contact us at{' '}
                <a href="mailto:privacy@omnifixlabs.com" className="text-primary-600 hover:text-primary-700">
                  privacy@omnifixlabs.com
                </a>
                . We currently handle these requests manually rather than through a self-serve tool - we'll confirm what
                we can do and how long it will take when you reach out.
              </p>
              <p>
                If you're an employee of a business using the Service rather than the business owner, some requests
                (like correcting a customer record) may need to go through that business instead of us directly, since
                they control that data.
              </p>
            </Section>

            <Section title="9. Cookies">
              <p>
                We use one essential cookie: an HTTP-only refresh token that keeps you signed in. We don't use
                advertising or cross-site tracking cookies.
              </p>
            </Section>

            <Section title="10. Children's privacy">
              <p>
                The Service is intended for business use and is not directed at children. We don't knowingly collect
                personal data from anyone under 18.
              </p>
            </Section>

            <Section title="11. International data transfers">
              <p>
                <span className="font-medium">
                  [placeholder: state where your servers and sub-processors are actually located, and what safeguard
                  applies if any of your users are in a region - e.g. the EU - with its own cross-border transfer
                  rules.]
                </span>
              </p>
            </Section>

            <Section title="12. Changes to this policy">
              <p>
                If we make a material change to this policy, we'll update the "Last updated" date above and, where
                required, notify account owners directly.
              </p>
            </Section>

            <Section title="13. Contact us">
              <p>
                Questions about this policy or how we handle your data?{' '}
                <a href="mailto:privacy@omnifixlabs.com" className="text-primary-600 hover:text-primary-700">
                  privacy@omnifixlabs.com
                </a>
              </p>
            </Section>
          </div>
        </div>

        <Link
          to="/login"
          className="mt-6 flex items-center justify-center gap-1.5 text-sm font-medium text-primary-600 hover:text-primary-700"
        >
          <ArrowLeft className="h-3.5 w-3.5" />
          Back to sign in
        </Link>
      </div>
    </div>
  )
}
