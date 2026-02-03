'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import {
  ExclamationTriangleIcon,
  InformationCircleIcon,
  KeyIcon,
  XMarkIcon,
} from '@heroicons/react/24/outline';
import type { AkahuAccount, SaveAkahuCredentialsResult } from '@/types/bank-connections';
import { useTranslations } from 'next-intl';

interface AkahuSetupDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (accounts: AkahuAccount[]) => void;
  credentialsError?: string;
}

export function AkahuSetupDialog({
  isOpen,
  onClose,
  onSuccess,
  credentialsError,
}: AkahuSetupDialogProps) {
  const t = useTranslations('bankConnections');
  const tCommon = useTranslations('common');
  const tToasts = useTranslations('toasts');
  const [appIdToken, setAppIdToken] = useState('');
  const [userToken, setUserToken] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!appIdToken.trim() || !userToken.trim()) {
      setError(t('tokensRequired'));
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const result: SaveAkahuCredentialsResult = await apiClient.saveAkahuCredentials({
        appIdToken: appIdToken.trim(),
        userToken: userToken.trim(),
      });

      if (result.isSuccess) {
        toast.success(tToasts('akahuCredentialsSaved'));
        // Clear form
        setAppIdToken('');
        setUserToken('');
        // Pass available accounts to parent
        onSuccess(result.availableAccounts || []);
      } else {
        setError(result.errorMessage || t('saveCredentialsFailed'));
      }
    } catch (err) {
      console.error('Failed to save Akahu credentials:', err);
      setError(t('connectAkahuFailed'));
    } finally {
      setIsLoading(false);
    }
  };

  const handleClose = () => {
    setAppIdToken('');
    setUserToken('');
    setError(null);
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="bg-white rounded-xl shadow-xl max-w-lg w-full mx-4 max-h-[80vh] overflow-hidden">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <KeyIcon className="w-5 h-5 text-primary-600" />
            <div>
              <h2 className="text-lg font-semibold text-gray-900">
                {t('setupTitle')}
              </h2>
              <p className="text-sm text-gray-500">
                {t('setupSubtitle')}
              </p>
            </div>
          </div>
          <button
            onClick={handleClose}
            className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
          >
            <XMarkIcon className="w-5 h-5 text-gray-500" />
          </button>
        </div>

        {/* Content */}
        <form onSubmit={handleSubmit} className="px-6 py-4 space-y-4">
          {/* Error display */}
          {(error || credentialsError) && (
            <div className="flex items-start gap-2 p-3 bg-red-50 border border-red-200 rounded-lg text-sm text-red-700">
              <ExclamationTriangleIcon className="w-5 h-5 text-red-500 shrink-0 mt-0.5" />
              <span>{error || credentialsError}</span>
            </div>
          )}

          {/* Instructions */}
          <div className="flex items-start gap-2 p-3 bg-blue-50 border border-blue-200 rounded-lg text-sm text-blue-700">
            <InformationCircleIcon className="w-5 h-5 text-blue-500 shrink-0 mt-0.5" />
            <div>
              <p className="font-medium">{t('whereToFindTokens')}</p>
              <ol className="mt-1 ml-4 list-decimal text-blue-600">
                <li>
                  Go to{' '}
                  <a
                    href="https://my.akahu.nz/developers"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="underline hover:text-blue-800"
                  >
                    my.akahu.nz/developers
                  </a>
                </li>
                <li>{t('createPersonalApp')}</li>
                <li>{t('copyTokens')}</li>
              </ol>
            </div>
          </div>

          {/* App Token */}
          <div className="space-y-2">
            <Label htmlFor="appIdToken">{t('appToken')}</Label>
            <Input
              id="appIdToken"
              type="password"
              placeholder={t('appTokenPlaceholder')}
              value={appIdToken}
              onChange={(e) => setAppIdToken(e.target.value)}
              disabled={isLoading}
              autoComplete="off"
            />
            <p className="text-xs text-gray-500">
              {t('appTokenHint')}
            </p>
          </div>

          {/* User Token */}
          <div className="space-y-2">
            <Label htmlFor="userToken">{t('userToken')}</Label>
            <Input
              id="userToken"
              type="password"
              placeholder={t('userTokenPlaceholder')}
              value={userToken}
              onChange={(e) => setUserToken(e.target.value)}
              disabled={isLoading}
              autoComplete="off"
            />
            <p className="text-xs text-gray-500">
              {t('userTokenHint')}
            </p>
          </div>

          {/* Footer */}
          <div className="flex justify-end gap-2 pt-2">
            <Button
              type="button"
              variant="outline"
              onClick={handleClose}
              disabled={isLoading}
            >
              {tCommon('cancel')}
            </Button>
            <Button type="submit" disabled={isLoading}>
              {isLoading ? t('verifying') : t('saveAndContinue')}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
