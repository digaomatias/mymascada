'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { useCallback, useEffect, useState } from 'react';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import Link from 'next/link';
import {
  ChatBubbleBottomCenterTextIcon,
  ArrowLeftIcon,
  CheckCircleIcon,
} from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import type { TelegramSettingsResponse, TelegramTestResult } from '@/types/telegram-settings';

export default function TelegramSettingsPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const t = useTranslations('settings.telegram');
  const tCommon = useTranslations('common');

  // Data state
  const [settings, setSettings] = useState<TelegramSettingsResponse | null>(null);
  const [loadingSettings, setLoadingSettings] = useState(true);

  // Form state
  const [botToken, setBotToken] = useState('');

  // Action state
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<TelegramTestResult | null>(null);
  const [connecting, setConnecting] = useState(false);
  const [showDisconnectConfirm, setShowDisconnectConfirm] = useState(false);
  const [disconnecting, setDisconnecting] = useState(false);

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  const loadSettings = useCallback(async () => {
    try {
      setLoadingSettings(true);
      const data = await apiClient.getTelegramSettings();
      setSettings(data?.hasSettings ? data : null);
    } catch (error) {
      console.error('Failed to load Telegram settings:', error);
      toast.error(t('errors.loadFailed'));
    } finally {
      setLoadingSettings(false);
    }
  }, [t]);

  useEffect(() => {
    if (isAuthenticated && !isLoading) {
      loadSettings();
    }
  }, [isAuthenticated, isLoading, loadSettings]);

  const handleTestToken = async () => {
    if (!botToken.trim()) {
      toast.error(t('errors.tokenRequired'));
      return;
    }

    setTesting(true);
    setTestResult(null);
    try {
      const result = await apiClient.testTelegramConnection({ botToken: botToken.trim() });
      setTestResult(result);
    } catch (error) {
      console.error('Token test failed:', error);
      setTestResult({
        success: false,
        error: (error as Error).message,
      });
    } finally {
      setTesting(false);
    }
  };

  const handleConnect = async () => {
    if (!botToken.trim()) {
      toast.error(t('errors.tokenRequired'));
      return;
    }

    setConnecting(true);
    try {
      const result = await apiClient.saveTelegramSettings({ botToken: botToken.trim() });
      setSettings(result);
      setBotToken('');
      setTestResult(null);
      toast.success(t('connected'));
    } catch (error) {
      console.error('Failed to connect Telegram bot:', error);
      toast.error(t('errors.connectFailed'));
    } finally {
      setConnecting(false);
    }
  };

  const handleDisconnect = async () => {
    setDisconnecting(true);
    try {
      await apiClient.deleteTelegramSettings();
      setSettings(null);
      setBotToken('');
      setTestResult(null);
      toast.success(t('disconnected'));
    } catch (error) {
      console.error('Failed to disconnect Telegram bot:', error);
      toast.error(t('errors.disconnectFailed'));
    } finally {
      setDisconnecting(false);
      setShowDisconnectConfirm(false);
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <ChatBubbleBottomCenterTextIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">{tCommon('loading')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return null;
  }

  return (
    <AppLayout>
      {/* Back link */}
        <Link
          href="/settings"
          className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 mb-4"
        >
          <ArrowLeftIcon className="w-4 h-4" />
          {t('backToSettings')}
        </Link>

        {/* Header */}
        <div className="mb-6 lg:mb-8">
          <div className="flex items-center gap-3 mb-1">
            <div className="w-10 h-10 bg-gradient-to-br from-primary-400 to-primary-600 rounded-xl flex items-center justify-center">
              <ChatBubbleBottomCenterTextIcon className="w-5 h-5 text-white" />
            </div>
            <div>
              <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900">
                {t('title')}
              </h1>
              <p className="text-gray-600 mt-0.5">{t('subtitle')}</p>
            </div>
          </div>
        </div>

        {loadingSettings ? (
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
              <div className="animate-pulse space-y-4">
                <div className="h-4 bg-gray-200 rounded w-1/3"></div>
                <div className="h-10 bg-gray-200 rounded"></div>
                <div className="h-4 bg-gray-200 rounded w-1/4"></div>
              </div>
            </CardContent>
          </Card>
        ) : settings?.hasSettings ? (
          /* State 2: Connected */
          <div className="space-y-4">
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardContent className="p-4">
                <div className="flex items-start gap-3">
                  <CheckCircleIcon className="w-5 h-5 text-green-600 shrink-0 mt-0.5" />
                  <div>
                    <p className="text-sm font-medium text-green-800">
                      {t('connectedAs', { username: settings.botUsername || '...' })}
                    </p>
                    {settings.isVerified && (
                      <p className="text-xs text-green-600 mt-0.5">
                        {t('verified')}
                        {settings.lastVerifiedAt && (
                          <> &middot; {t('lastVerified', { date: new Date(settings.lastVerifiedAt).toLocaleDateString() })}</>
                        )}
                      </p>
                    )}
                  </div>
                </div>
              </CardContent>
            </Card>

            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardContent className="p-6">
                <p className="text-sm text-gray-600 mb-4">{t('connectedDescription')}</p>
                <Button
                  variant="danger"
                  onClick={() => setShowDisconnectConfirm(true)}
                  disabled={disconnecting}
                >
                  {t('disconnect')}
                </Button>
              </CardContent>
            </Card>
          </div>
        ) : (
          /* State 1: Not configured (wizard) */
          <div className="space-y-4">
            {/* Instructions */}
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardContent className="p-6">
                <h2 className="text-lg font-semibold text-gray-900 mb-3">{t('setup.title')}</h2>
                <ol className="list-decimal list-inside space-y-2 text-sm text-gray-700">
                  <li>{t('setup.step1')}</li>
                  <li>{t('setup.step2')}</li>
                  <li>{t('setup.step3')}</li>
                  <li>{t('setup.step4')}</li>
                </ol>
                <a
                  href="https://t.me/BotFather"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-block mt-3 text-sm text-primary-600 hover:text-primary-800 font-medium"
                >
                  {t('setup.openBotFather')}
                </a>
              </CardContent>
            </Card>

            {/* Token Input & Actions */}
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardContent className="p-6 space-y-5">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    {t('token.label')}
                  </label>
                  <Input
                    type="password"
                    value={botToken}
                    onChange={(e) => {
                      setBotToken(e.target.value);
                      setTestResult(null);
                    }}
                    placeholder={t('token.placeholder')}
                    className="w-full"
                  />
                  <p className="text-xs text-gray-500 mt-1.5">{t('token.hint')}</p>
                </div>

                {/* Test Token */}
                <div>
                  <Button
                    variant="secondary"
                    onClick={handleTestToken}
                    loading={testing}
                    disabled={testing || !botToken.trim()}
                    className="w-full sm:w-auto"
                  >
                    {testing ? t('testing') : t('testToken')}
                  </Button>

                  {testResult && (
                    <div
                      className={`mt-3 p-3 rounded-lg text-sm ${
                        testResult.success
                          ? 'bg-green-50 border border-green-200 text-green-800'
                          : 'bg-red-50 border border-red-200 text-red-800'
                      }`}
                    >
                      {testResult.success
                        ? t('testSuccess', { username: testResult.botUsername || '' })
                        : t('testError', { error: testResult.error || 'Unknown error' })}
                    </div>
                  )}
                </div>

                {/* Connect */}
                <div className="pt-3 border-t border-gray-100">
                  <Button
                    variant="primary"
                    onClick={handleConnect}
                    loading={connecting}
                    disabled={connecting || !botToken.trim()}
                    className="w-full sm:w-auto"
                  >
                    {connecting ? t('connecting') : t('connect')}
                  </Button>
                </div>
              </CardContent>
            </Card>
          </div>
        )}

      <ConfirmationDialog
        isOpen={showDisconnectConfirm}
        onClose={() => setShowDisconnectConfirm(false)}
        onConfirm={handleDisconnect}
        title={t('disconnect')}
        description={t('disconnectConfirm')}
        confirmText={tCommon('confirm')}
        cancelText={tCommon('cancel')}
        variant="danger"
      />
    </AppLayout>
  );
}

export const dynamic = 'force-dynamic';
