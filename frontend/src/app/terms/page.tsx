import Link from 'next/link';

export default function TermsOfServicePage() {
  return (
    <div className="min-h-screen bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-3xl mx-auto">
        <div className="mb-8">
          <Link href="/" className="text-sm text-primary hover:text-primary-600">
            &larr; Back to MyMascada
          </Link>
        </div>

        <div className="bg-white rounded-lg shadow-sm p-8 sm:p-12 prose prose-gray max-w-none">
          <h1 className="text-3xl font-bold text-gray-900 mb-2">Terms of Service</h1>
          <p className="text-sm text-gray-500 mb-8">Last updated: February 2026</p>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">What is MyMascada?</h2>
          <p className="text-gray-700 mb-4">
            MyMascada is a personal finance management tool. The instance hosted at MyMascada.com is run as
            a personal hobby project. It is not a commercial product, and there is no company behind it &mdash;
            just a developer who built something useful and made it available online.
          </p>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">No Warranty</h2>
          <p className="text-gray-700 mb-4">
            This service is provided <strong>&quot;as-is&quot;</strong> and <strong>&quot;as-available&quot;</strong>,
            without any warranties of any kind, express or implied. There is no guarantee of uptime, availability,
            data preservation, or that the service will continue to exist. Things may break. Downtime may happen.
            Features may change without notice.
          </p>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Not Financial Advice</h2>
          <p className="text-gray-700 mb-4">
            MyMascada is a tool for tracking and organizing your personal finances. Nothing in this application
            constitutes financial advice, investment advice, tax advice, or any other form of professional advice.
            Any AI-generated categorizations or insights are for informational purposes only. You are solely
            responsible for your financial decisions. Always consult a qualified professional for financial,
            tax, or legal matters.
          </p>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Limitation of Liability</h2>
          <p className="text-gray-700 mb-4">
            To the fullest extent permitted by law, the operator of this service shall not be held liable for any
            damages, losses, or costs arising from your use of (or inability to use) MyMascada. This includes,
            but is not limited to, data loss, financial losses, or any indirect or consequential damages.
          </p>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Your Responsibilities</h2>
          <ul className="list-disc pl-6 text-gray-700 mb-4 space-y-2">
            <li>You are responsible for the accuracy and security of your own data.</li>
            <li>You are responsible for keeping your account credentials safe.</li>
            <li>You agree not to use the service for any illegal or unauthorized purpose.</li>
            <li>You agree not to attempt to access other users&apos; data or disrupt the service.</li>
          </ul>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Your Rights</h2>
          <p className="text-gray-700 mb-4">
            You retain ownership of all data you enter into MyMascada. You have the right to:
          </p>
          <ul className="list-disc pl-6 text-gray-700 mb-4 space-y-2">
            <li><strong>Access</strong> all your personal data at any time through the application.</li>
            <li><strong>Export</strong> your transaction and financial data.</li>
            <li><strong>Delete</strong> your account and all associated data from your account settings.</li>
            <li><strong>Rectify</strong> any inaccurate data by editing it within the application.</li>
          </ul>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Account Termination</h2>
          <p className="text-gray-700 mb-4">
            The operator reserves the right to suspend or terminate any account, or discontinue the service
            entirely, at any time and for any reason, with or without notice. If you want to delete your
            account and data, you can do so from your account settings.
          </p>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Data Handling</h2>
          <p className="text-gray-700 mb-4">
            For details on how your data is collected, used, and stored, please refer to
            the <Link href="/privacy" className="text-primary hover:text-primary-600 underline">Privacy Policy</Link>.
          </p>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Third-Party Services</h2>
          <p className="text-gray-700 mb-4">
            MyMascada may integrate with third-party services to provide its features. These include, but
            are not limited to:
          </p>
          <ul className="list-disc pl-6 text-gray-700 mb-4 space-y-2">
            <li><strong>OpenAI</strong> &mdash; for AI-powered transaction categorization (optional, only when enabled).</li>
            <li><strong>Stripe</strong> &mdash; for payment processing (optional, only if subscription features are used).</li>
            <li><strong>Akahu</strong> &mdash; for bank account syncing in New Zealand (optional, only when connected).</li>
            <li><strong>Google</strong> &mdash; for OAuth sign-in (optional, only if you choose &quot;Sign in with Google&quot;).</li>
          </ul>
          <p className="text-gray-700 mb-4">
            Your use of these integrations is subject to each provider&apos;s own terms and privacy policies.
            Data is only shared with these services when you actively use the corresponding feature. See
            our <Link href="/privacy" className="text-primary hover:text-primary-600 underline">Privacy Policy</Link> for
            full details.
          </p>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">GDPR &amp; Data Protection</h2>
          <p className="text-gray-700 mb-4">
            If you are located in the European Economic Area (EEA), you have additional rights under the
            General Data Protection Regulation (GDPR), including the right to access, rectify, port, and
            erase your personal data, as well as the right to restrict or object to certain processing.
            To exercise these rights, contact us at{' '}
            <a href="mailto:support@mymascada.com" className="text-primary hover:text-primary-600 underline">
              support@mymascada.com
            </a>.
          </p>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Open Source</h2>
          <p className="text-gray-700 mb-4">
            MyMascada is open-source software licensed under
            the <strong>GNU Affero General Public License v3.0 (AGPL-3.0)</strong>. The source code is available
            on <a href="https://github.com/digaomatias/mymascada" target="_blank" rel="noopener noreferrer" className="text-primary hover:text-primary-600 underline">GitHub</a>.
            You are free to self-host your own instance under the terms of that license.
          </p>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Changes to These Terms</h2>
          <p className="text-gray-700 mb-4">
            These terms may be updated from time to time. Continued use of the service after changes
            constitutes acceptance of the updated terms.
          </p>

          <hr className="my-8 border-gray-200" />

          <p className="text-sm text-gray-500">
            Questions? Reach out at{' '}
            <a href="mailto:support@mymascada.com" className="text-primary hover:text-primary-600 underline">
              support@mymascada.com
            </a>{' '}
            or via the{' '}
            <a href="https://github.com/digaomatias/mymascada/discussions" target="_blank" rel="noopener noreferrer" className="text-primary hover:text-primary-600 underline">
              GitHub Discussions
            </a> page.
          </p>
        </div>
      </div>
    </div>
  );
}
