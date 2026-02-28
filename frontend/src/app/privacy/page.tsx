import Link from 'next/link';

export default function PrivacyPolicyPage() {
  return (
    <div className="min-h-screen bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-3xl mx-auto">
        <div className="mb-8">
          <Link href="/" className="text-sm text-primary hover:text-primary-600">
            &larr; Back to MyMascada
          </Link>
        </div>

        <div className="bg-white rounded-lg shadow-sm p-8 sm:p-12 prose prose-gray max-w-none">
          <h1 className="text-3xl font-bold text-gray-900 mb-2">Privacy Policy</h1>
          <p className="text-sm text-gray-500 mb-8">Last updated: February 2026</p>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Overview</h2>
          <p className="text-gray-700 mb-4">
            MyMascada.com is a hosted instance of MyMascada, an open-source personal finance application.
            This privacy policy explains what data is collected, how it is used, and what third-party
            services are involved when you use MyMascada.com.
          </p>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Data We Store</h2>
          <p className="text-gray-700 mb-2">
            When you create an account and use MyMascada, the following data is stored in our database:
          </p>
          <ul className="list-disc pl-6 text-gray-700 mb-4 space-y-2">
            <li>User account information (name, email, hashed password)</li>
            <li>Financial transactions and account balances</li>
            <li>Categories, budgets, and categorization rules</li>
            <li>Application settings and preferences</li>
          </ul>
          <p className="text-gray-700 mb-4">
            Your data is stored on secured infrastructure and is not sold, shared with, or disclosed to
            any third party, except as described below for the optional services you choose to enable.
          </p>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Telemetry</h2>
          <p className="text-gray-700 mb-4">
            MyMascada does <strong>not</strong> include any telemetry, analytics, or usage tracking.
            No data is sent to any analytics service. There are no cookies for advertising or tracking purposes.
          </p>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Third-Party Services</h2>
          <p className="text-gray-700 mb-4">
            The following external services may be used depending on the features you enable.
            Data is only shared with these providers when you actively use the corresponding feature.
          </p>

          <h3 className="text-lg font-semibold text-gray-900 mt-6 mb-3">Akahu (Bank Syncing &mdash; New Zealand)</h3>
          <ul className="list-disc pl-6 text-gray-700 mb-4 space-y-1">
            <li><strong>What is shared:</strong> Bank account information and transaction data are synced through Akahu&apos;s API.</li>
            <li><strong>When:</strong> Only when you connect a bank account via Akahu.</li>
            <li>
              <strong>Their policy:</strong>{' '}
              <a href="https://www.akahu.nz/privacy" target="_blank" rel="noopener noreferrer" className="text-primary hover:text-primary-600 underline">
                Akahu Privacy Policy
              </a>
            </li>
          </ul>

          <h3 className="text-lg font-semibold text-gray-900 mt-6 mb-3">Google OAuth (Sign-In)</h3>
          <ul className="list-disc pl-6 text-gray-700 mb-4 space-y-1">
            <li><strong>What is shared:</strong> Standard OAuth flow &mdash; Google provides your email address and profile name for authentication.</li>
            <li><strong>When:</strong> Only if you choose &quot;Sign in with Google&quot;.</li>
            <li>
              <strong>Their policy:</strong>{' '}
              <a href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer" className="text-primary hover:text-primary-600 underline">
                Google Privacy Policy
              </a>
            </li>
          </ul>

          <h3 className="text-lg font-semibold text-gray-900 mt-6 mb-3">OpenAI API (AI Categorization)</h3>
          <ul className="list-disc pl-6 text-gray-700 mb-4 space-y-1">
            <li><strong>What is shared:</strong> Transaction descriptions and amounts are sent to OpenAI for categorization suggestions.</li>
            <li><strong>When:</strong> Only when AI-powered categorization is triggered during transaction review.</li>
            <li><strong>Data retention:</strong> OpenAI API usage data is subject to OpenAI&apos;s data retention policies. We use the API with zero data retention where available.</li>
            <li>
              <strong>Their policy:</strong>{' '}
              <a href="https://openai.com/privacy/" target="_blank" rel="noopener noreferrer" className="text-primary hover:text-primary-600 underline">
                OpenAI Privacy Policy
              </a>
            </li>
          </ul>

          <h3 className="text-lg font-semibold text-gray-900 mt-6 mb-3">Stripe (Payment Processing)</h3>
          <ul className="list-disc pl-6 text-gray-700 mb-4 space-y-1">
            <li><strong>What is shared:</strong> Email address and payment information are processed by Stripe for subscription management.</li>
            <li><strong>When:</strong> Only if you subscribe to a paid plan or make a payment.</li>
            <li><strong>Note:</strong> We do not store your full credit card details. All payment data is handled securely by Stripe.</li>
            <li>
              <strong>Their policy:</strong>{' '}
              <a href="https://stripe.com/privacy" target="_blank" rel="noopener noreferrer" className="text-primary hover:text-primary-600 underline">
                Stripe Privacy Policy
              </a>
            </li>
          </ul>

          <h3 className="text-lg font-semibold text-gray-900 mt-6 mb-3">Email Provider (Transactional Emails)</h3>
          <ul className="list-disc pl-6 text-gray-700 mb-4 space-y-1">
            <li><strong>What is shared:</strong> Your email address and notification content are sent through our email provider.</li>
            <li><strong>When:</strong> For email verification, password resets, and notifications.</li>
          </ul>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Data Retention</h2>
          <p className="text-gray-700 mb-4">
            Your data is retained for as long as your account is active. If you delete your account,
            your data will be permanently removed from our systems. Backups containing your data may
            persist for up to 30 days after deletion before being fully purged.
          </p>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Your Rights</h2>
          <ul className="list-disc pl-6 text-gray-700 mb-4 space-y-2">
            <li><strong>Access:</strong> All your data is visible to you within the application at all times.</li>
            <li><strong>Export:</strong> You can export your transaction data from within the application.</li>
            <li><strong>Rectification:</strong> You can edit and correct your data directly in the application.</li>
            <li><strong>Deletion:</strong> You can delete your account and all associated data from your account settings.</li>
            <li><strong>Portability:</strong> You can export your data in standard formats for use with other services.</li>
          </ul>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">GDPR &amp; European Users</h2>
          <p className="text-gray-700 mb-4">
            If you are located in the European Economic Area (EEA), you have rights under the General
            Data Protection Regulation (GDPR). In addition to the rights listed above, you may:
          </p>
          <ul className="list-disc pl-6 text-gray-700 mb-4 space-y-2">
            <li><strong>Object</strong> to certain types of data processing.</li>
            <li><strong>Restrict</strong> the processing of your personal data in specific circumstances.</li>
            <li><strong>Lodge a complaint</strong> with your local data protection authority.</li>
          </ul>
          <p className="text-gray-700 mb-4">
            The legal basis for processing your data is your consent (provided at registration) and the
            legitimate interest of providing the service you signed up for. To exercise any of these rights,
            contact us at{' '}
            <a href="mailto:support@mymascada.com" className="text-primary hover:text-primary-600 underline">
              support@mymascada.com
            </a>.
          </p>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Security</h2>
          <p className="text-gray-700 mb-4">
            We take reasonable measures to protect your data, including encrypted connections (HTTPS),
            hashed passwords, and access controls. However, no system is 100% secure, and we cannot
            guarantee absolute security.
          </p>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Changes to This Policy</h2>
          <p className="text-gray-700 mb-4">
            This privacy policy may be updated from time to time. Continued use of the service after
            changes constitutes acceptance of the updated policy.
          </p>

          <hr className="my-8 border-gray-200" />

          <p className="text-sm text-gray-500">
            Questions about your data? Reach out at{' '}
            <a href="mailto:support@mymascada.com" className="text-primary hover:text-primary-600 underline">
              support@mymascada.com
            </a>{' '}
            or via the{' '}
            <a href="https://github.com/digaomatias/mymascada/discussions" target="_blank" rel="noopener noreferrer" className="text-primary hover:text-primary-600 underline">
              GitHub Discussions
            </a> page. Also see our <Link href="/terms" className="text-primary hover:text-primary-600 underline">Terms of Service</Link>.
          </p>
        </div>
      </div>
    </div>
  );
}
