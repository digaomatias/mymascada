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
          <p className="text-sm text-gray-500 mb-8">Last updated: February 2025</p>

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
            <li>
              <strong>Their policy:</strong>{' '}
              <a href="https://openai.com/privacy/" target="_blank" rel="noopener noreferrer" className="text-primary hover:text-primary-600 underline">
                OpenAI Privacy Policy
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
            your data will be removed from our systems.
          </p>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Your Rights</h2>
          <ul className="list-disc pl-6 text-gray-700 mb-4 space-y-2">
            <li><strong>Export:</strong> You can export your transaction data from within the application.</li>
            <li><strong>Deletion:</strong> You can delete your account and all associated data from your account settings.</li>
            <li><strong>Access:</strong> All your data is visible to you within the application at all times.</li>
          </ul>

          <h2 className="text-xl font-semibold text-gray-900 mt-8 mb-4">Changes to This Policy</h2>
          <p className="text-gray-700 mb-4">
            This privacy policy may be updated from time to time. Continued use of the service after
            changes constitutes acceptance of the updated policy.
          </p>

          <hr className="my-8 border-gray-200" />

          <p className="text-sm text-gray-500">
            Questions about your data? Reach out via the{' '}
            <a href="https://github.com/digaomatias/mymascada/discussions" target="_blank" rel="noopener noreferrer" className="text-primary hover:text-primary-600 underline">
              GitHub Discussions
            </a> page. Also see our <Link href="/terms" className="text-primary hover:text-primary-600 underline">Terms of Service</Link>.
          </p>
        </div>
      </div>
    </div>
  );
}
