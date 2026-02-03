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
          <p className="text-sm text-gray-500 mb-8">Last updated: February 2025</p>

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
            You are solely responsible for your financial decisions.
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
            Questions? Reach out via the{' '}
            <a href="https://github.com/digaomatias/mymascada/discussions" target="_blank" rel="noopener noreferrer" className="text-primary hover:text-primary-600 underline">
              GitHub Discussions
            </a> page.
          </p>
        </div>
      </div>
    </div>
  );
}
