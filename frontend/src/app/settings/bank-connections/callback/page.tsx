'use client';

import { useEffect, useState, Suspense } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import {
  BuildingLibraryIcon,
  CheckCircleIcon,
  ExclamationCircleIcon
} from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';

type CallbackStatus = 'processing' | 'success' | 'error';

function CallbackContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const t = useTranslations('settings.bankConnections.callback');
  const [status, setStatus] = useState<CallbackStatus>('processing');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    const handleCallback = async () => {
      const code = searchParams.get('code');
      const state = searchParams.get('state');
      const error = searchParams.get('error');
      const errorDescription = searchParams.get('error_description');

      // Handle OAuth error
      if (error) {
        setStatus('error');
        setErrorMessage(errorDescription || error || t('errors.denied'));
        toast.error(t('failed') + ': ' + (errorDescription || error));
        return;
      }

      // Validate required params
      if (!code || !state) {
        setStatus('error');
        setErrorMessage(t('errors.missingParams'));
        return;
      }

      // Verify state matches what we stored
      const storedState = localStorage.getItem('akahu_oauth_state');
      if (!storedState || storedState !== state) {
        setStatus('error');
        setErrorMessage(t('errors.securityFailed'));
        return;
      }

      try {
        // Exchange the code for an access token and get available accounts
        const result = await apiClient.exchangeAkahuCode(code, state);

        // Store the access token and accounts for the bank connections page
        localStorage.setItem('akahu_access_token', result.accessToken);
        localStorage.setItem('akahu_available_accounts', JSON.stringify(result.accounts));

        setStatus('success');

        // Redirect to bank connections page after a short delay
        setTimeout(() => {
          router.push('/settings/bank-connections');
        }, 1500);
      } catch (err) {
        console.error('Callback processing failed:', err);
        setStatus('error');
        setErrorMessage(t('errors.processingFailed'));
      }
    };

    handleCallback();
  }, [searchParams, router]);

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-xl p-8 max-w-md w-full text-center">
        {status === 'processing' && (
          <>
            <div className="w-16 h-16 bg-gradient-to-br from-blue-400 to-blue-600 rounded-2xl flex items-center justify-center mx-auto animate-pulse">
              <BuildingLibraryIcon className="w-8 h-8 text-white" />
            </div>
            <h1 className="mt-6 text-xl font-semibold text-gray-900">
              {t('connecting')}
            </h1>
            <p className="mt-2 text-gray-600">
              {t('pleaseWait')}
            </p>
            <div className="mt-6 flex justify-center">
              <div className="w-8 h-8 border-4 border-blue-200 border-t-blue-600 rounded-full animate-spin" />
            </div>
          </>
        )}

        {status === 'success' && (
          <>
            <div className="w-16 h-16 bg-gradient-to-br from-green-400 to-green-600 rounded-2xl flex items-center justify-center mx-auto">
              <CheckCircleIcon className="w-8 h-8 text-white" />
            </div>
            <h1 className="mt-6 text-xl font-semibold text-gray-900">
              {t('authorized')}
            </h1>
            <p className="mt-2 text-gray-600">
              {t('redirecting')}
            </p>
          </>
        )}

        {status === 'error' && (
          <>
            <div className="w-16 h-16 bg-gradient-to-br from-red-400 to-red-600 rounded-2xl flex items-center justify-center mx-auto">
              <ExclamationCircleIcon className="w-8 h-8 text-white" />
            </div>
            <h1 className="mt-6 text-xl font-semibold text-gray-900">
              {t('failed')}
            </h1>
            <p className="mt-2 text-gray-600">
              {errorMessage || t('unexpectedError')}
            </p>
            <button
              onClick={() => router.push('/settings/bank-connections')}
              className="mt-6 px-6 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 transition-colors"
            >
              {t('tryAgain')}
            </button>
          </>
        )}
      </div>
    </div>
  );
}

function CallbackFallback() {
  const t = useTranslations('settings.bankConnections.callback');

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-xl p-8 max-w-md w-full text-center">
        <div className="w-16 h-16 bg-gradient-to-br from-blue-400 to-blue-600 rounded-2xl flex items-center justify-center mx-auto animate-pulse">
          <BuildingLibraryIcon className="w-8 h-8 text-white" />
        </div>
        <h1 className="mt-6 text-xl font-semibold text-gray-900">
          {t('loading')}
        </h1>
      </div>
    </div>
  );
}

export default function AkahuCallbackPage() {
  return (
    <Suspense fallback={<CallbackFallback />}>
      <CallbackContent />
    </Suspense>
  );
}

export const dynamic = 'force-dynamic';
