'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { useCallback, useEffect, useState } from 'react';
import Navigation from '@/components/navigation';
import { Card, CardContent } from '@/components/ui/card';
import { Select } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import Link from 'next/link';
import {
  SparklesIcon,
  ArrowLeftIcon,
  CheckCircleIcon,
  ExclamationCircleIcon,
  InformationCircleIcon,
} from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import type {
  AiSettingsResponse,
  AiProviderPreset,
  AiConnectionTestResult,
} from '@/types/ai-settings';

type AiPurpose = 'general' | 'chat';

export default function AiSettingsPage() {
  const { isAuthenticated, isLoading, user, refreshUser } = useAuth();
  const router = useRouter();
  const t = useTranslations('settings.ai');
  const tCommon = useTranslations('common');

  // Tab state
  const [activePurpose, setActivePurpose] = useState<AiPurpose>('general');

  // Data state
  const [settings, setSettings] = useState<AiSettingsResponse | null>(null);
  const [providers, setProviders] = useState<AiProviderPreset[]>([]);
  const [loadingSettings, setLoadingSettings] = useState(true);
  const [loadingProviders, setLoadingProviders] = useState(true);

  // Form state
  const [selectedProviderId, setSelectedProviderId] = useState('');
  const [apiKey, setApiKey] = useState('');
  const [apiKeyChanged, setApiKeyChanged] = useState(false);
  const [modelId, setModelId] = useState('');
  const [customModel, setCustomModel] = useState('');
  const [apiEndpoint, setApiEndpoint] = useState('');

  // Action state
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<AiConnectionTestResult | null>(null);
  const [showRemoveConfirm, setShowRemoveConfirm] = useState(false);
  const [removing, setRemoving] = useState(false);

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  const resetFormState = useCallback(() => {
    setSelectedProviderId('');
    setApiKey('');
    setApiKeyChanged(false);
    setModelId('');
    setCustomModel('');
    setApiEndpoint('');
    setTestResult(null);
  }, []);

  const loadSettings = useCallback(async (purpose: AiPurpose) => {
    try {
      setLoadingSettings(true);
      resetFormState();
      const data = await apiClient.getAiSettings(purpose);
      setSettings(data?.hasSettings ? data : null);
      if (data?.hasSettings) {
        setSelectedProviderId(data.providerName);
        setModelId(data.modelId);
        if (data.apiEndpoint) {
          setApiEndpoint(data.apiEndpoint);
        }
      }
    } catch (error) {
      console.error('Failed to load AI settings:', error);
      toast.error(t('errors.loadFailed'));
    } finally {
      setLoadingSettings(false);
    }
  }, [t, resetFormState]);

  const loadProviders = useCallback(async () => {
    try {
      setLoadingProviders(true);
      const data = await apiClient.getAiProviders();
      setProviders(data);
    } catch (error) {
      console.error('Failed to load AI providers:', error);
      toast.error(t('errors.loadProvidersFailed'));
    } finally {
      setLoadingProviders(false);
    }
  }, [t]);

  useEffect(() => {
    if (isAuthenticated && !isLoading) {
      loadSettings(activePurpose);
      loadProviders();
    }
  }, [isAuthenticated, isLoading, activePurpose, loadSettings, loadProviders]);

  // When both settings and providers are loaded, detect custom model
  useEffect(() => {
    if (!providers.length || !selectedProviderId || !modelId) return;

    const provider = providers.find((p) => p.id === selectedProviderId);
    if (provider && provider.models.length > 0) {
      const isPreset = provider.models.some((m) => m.id === modelId);
      if (!isPreset) {
        setCustomModel(modelId);
        setModelId('');
      }
    }
  }, [providers, selectedProviderId, modelId]);

  const handleTabChange = (purpose: AiPurpose) => {
    if (purpose !== activePurpose) {
      setActivePurpose(purpose);
    }
  };

  const selectedProvider = providers.find((p) => p.id === selectedProviderId);
  const isStandardOpenAI = selectedProvider?.providerType === 'openai';
  const effectiveModel = customModel || modelId;

  const handleProviderChange = (providerId: string) => {
    setSelectedProviderId(providerId);
    const provider = providers.find((p) => p.id === providerId);
    if (provider) {
      // Auto-fill endpoint
      if (provider.defaultEndpoint) {
        setApiEndpoint(provider.defaultEndpoint);
      } else {
        setApiEndpoint('');
      }
      // Auto-select first model
      if (provider.models.length > 0) {
        setModelId(provider.models[0].id);
        setCustomModel('');
      } else {
        setModelId('');
      }
    }
    setTestResult(null);
  };

  const handleModelChange = (value: string) => {
    if (value === '__custom__') {
      setModelId('');
      setCustomModel('');
    } else {
      setModelId(value);
      setCustomModel('');
    }
    setTestResult(null);
  };

  const handleTestConnection = async () => {
    if (!selectedProvider) return;

    const keyToTest = apiKeyChanged ? apiKey : '';
    if (!keyToTest && !settings?.hasApiKey) {
      toast.error(t('errors.apiKeyRequired'));
      return;
    }
    if (!effectiveModel) {
      toast.error(t('errors.modelRequired'));
      return;
    }

    setTesting(true);
    setTestResult(null);
    try {
      const result = await apiClient.testAiConnection({
        providerType: selectedProvider.providerType,
        apiKey: keyToTest,
        modelId: effectiveModel,
        apiEndpoint: apiEndpoint || undefined,
      }, activePurpose);
      setTestResult(result);
    } catch (error) {
      console.error('Connection test failed:', error);
      setTestResult({
        success: false,
        error: (error as Error).message,
      });
    } finally {
      setTesting(false);
    }
  };

  const handleSave = async () => {
    if (!selectedProviderId) {
      toast.error(t('errors.providerRequired'));
      return;
    }
    if (!apiKeyChanged && !settings?.hasApiKey) {
      toast.error(t('errors.apiKeyRequired'));
      return;
    }
    if (!effectiveModel) {
      toast.error(t('errors.modelRequired'));
      return;
    }

    const provider = providers.find((p) => p.id === selectedProviderId);
    if (!provider) return;

    setSaving(true);
    try {
      const updated = await apiClient.updateAiSettings({
        providerType: provider.providerType,
        providerName: selectedProviderId,
        apiKey: apiKeyChanged ? apiKey : undefined,
        modelId: effectiveModel,
        apiEndpoint: apiEndpoint || undefined,
      }, activePurpose);
      setSettings(updated);
      setApiKey('');
      setApiKeyChanged(false);
      toast.success(t('saved'));
      await refreshUser();
    } catch (error) {
      console.error('Failed to save AI settings:', error);
      toast.error(t('errors.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  const handleRemove = async () => {
    setRemoving(true);
    try {
      await apiClient.deleteAiSettings(activePurpose);
      setSettings(null);
      resetFormState();
      toast.success(t('removed'));
      await refreshUser();
    } catch (error) {
      console.error('Failed to remove AI settings:', error);
      toast.error(t('errors.removeFailed'));
    } finally {
      setRemoving(false);
      setShowRemoveConfirm(false);
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-primary-500 to-primary-700 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <SparklesIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">{tCommon('loading')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return null;
  }

  const isLoadingData = loadingSettings || loadingProviders;

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
      <Navigation />

      <main className="container-responsive py-4 sm:py-6 lg:py-8 pb-24 md:pb-8">
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
              <SparklesIcon className="w-5 h-5 text-white" />
            </div>
            <div>
              <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900">
                {t('title')}
              </h1>
              <p className="text-gray-600 mt-0.5">{t('subtitle')}</p>
            </div>
          </div>
        </div>

        {/* Tab Switcher */}
        <div className="mb-4">
          <div className="flex border-b border-gray-200">
            <button
              onClick={() => handleTabChange('general')}
              className={`px-4 py-2.5 text-sm font-medium border-b-2 transition-colors cursor-pointer ${
                activePurpose === 'general'
                  ? 'border-primary-600 text-primary-700'
                  : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
              }`}
            >
              {t('tabs.general')}
            </button>
            <button
              onClick={() => handleTabChange('chat')}
              className={`px-4 py-2.5 text-sm font-medium border-b-2 transition-colors cursor-pointer ${
                activePurpose === 'chat'
                  ? 'border-primary-600 text-primary-700'
                  : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
              }`}
            >
              {t('tabs.chat')}
            </button>
          </div>
        </div>

        {/* Chat AI Description */}
        {activePurpose === 'chat' && (
          <div className="mb-4">
            <Card className="bg-blue-50/80 backdrop-blur-xs border-0 shadow-sm">
              <CardContent className="p-4">
                <div className="flex items-start gap-3">
                  <InformationCircleIcon className="w-5 h-5 text-blue-500 shrink-0 mt-0.5" />
                  <p className="text-sm text-blue-800">{t('chatDescription')}</p>
                </div>
              </CardContent>
            </Card>
          </div>
        )}

        {isLoadingData ? (
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
              <div className="animate-pulse space-y-4">
                <div className="h-4 bg-gray-200 rounded w-1/3"></div>
                <div className="h-10 bg-gray-200 rounded"></div>
                <div className="h-4 bg-gray-200 rounded w-1/4"></div>
                <div className="h-10 bg-gray-200 rounded"></div>
                <div className="h-4 bg-gray-200 rounded w-1/4"></div>
                <div className="h-10 bg-gray-200 rounded"></div>
              </div>
            </CardContent>
          </Card>
        ) : (
          <div className="space-y-4">
            {/* Status Banner */}
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardContent className="p-4">
                {settings ? (
                  <div className="flex items-start gap-3">
                    <CheckCircleIcon className="w-5 h-5 text-green-600 shrink-0 mt-0.5" />
                    <div>
                      <p className="text-sm font-medium text-green-800">{t('hasConfig')}</p>
                      {settings.isValidated ? (
                        <p className="text-xs text-green-600 mt-0.5">
                          {t('validated')}
                          {settings.lastValidatedAt && (
                            <> &middot; {t('lastValidated', { date: new Date(settings.lastValidatedAt).toLocaleDateString() })}</>
                          )}
                        </p>
                      ) : (
                        <p className="text-xs text-amber-600 mt-0.5">{t('notValidated')}</p>
                      )}
                    </div>
                  </div>
                ) : user?.hasAiConfigured === false ? (
                  <div className="flex items-start gap-3">
                    <ExclamationCircleIcon className="w-5 h-5 text-amber-500 shrink-0 mt-0.5" />
                    <p className="text-sm text-amber-800">{t('noConfig')}</p>
                  </div>
                ) : (
                  <div className="flex items-start gap-3">
                    <InformationCircleIcon className="w-5 h-5 text-blue-500 shrink-0 mt-0.5" />
                    <p className="text-sm text-blue-800">{t('serverDefault')}</p>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Configuration Form */}
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardContent className="p-6 space-y-5">
                {/* Provider */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    {t('provider.label')}
                  </label>
                  <Select
                    value={selectedProviderId}
                    onChange={(e) => handleProviderChange(e.target.value)}
                    placeholder={t('provider.placeholder')}
                    className="w-full"
                  >
                    <option value="" disabled>
                      {t('provider.placeholder')}
                    </option>
                    {providers.map((provider) => (
                      <option key={provider.id} value={provider.id}>
                        {provider.name}
                      </option>
                    ))}
                  </Select>
                </div>

                {/* API Key */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    {t('apiKey.label')}
                  </label>
                  {settings?.hasApiKey && !apiKeyChanged ? (
                    <div className="flex items-center gap-3">
                      <div className="flex-1 input bg-gray-50 text-gray-500 flex items-center">
                        <span>{t('apiKey.current')}: ****{settings.apiKeyLastFour}</span>
                      </div>
                      <Button
                        variant="secondary"
                        size="sm"
                        onClick={() => setApiKeyChanged(true)}
                      >
                        {t('apiKey.change')}
                      </Button>
                    </div>
                  ) : (
                    <Input
                      type="password"
                      value={apiKey}
                      onChange={(e) => {
                        setApiKey(e.target.value);
                        setApiKeyChanged(true);
                      }}
                      placeholder={t('apiKey.placeholder')}
                      className="w-full"
                    />
                  )}
                  <p className="text-xs text-gray-500 mt-1.5">{t('apiKey.hint')}</p>
                </div>

                {/* Model */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    {t('model.label')}
                  </label>
                  {selectedProvider && selectedProvider.models.length > 0 ? (
                    <div className="space-y-2">
                      <Select
                        value={customModel ? '__custom__' : modelId}
                        onChange={(e) => handleModelChange(e.target.value)}
                        className="w-full"
                      >
                        {selectedProvider.models.map((model) => (
                          <option key={model.id} value={model.id}>
                            {model.name}
                          </option>
                        ))}
                        <option value="__custom__">{t('model.custom')}</option>
                      </Select>
                      {(customModel !== '' || (!modelId && customModel === '')) && (
                        <Input
                          type="text"
                          value={customModel}
                          onChange={(e) => setCustomModel(e.target.value)}
                          placeholder={t('model.placeholder')}
                          className="w-full"
                        />
                      )}
                    </div>
                  ) : (
                    <Input
                      type="text"
                      value={customModel || modelId}
                      onChange={(e) => {
                        setCustomModel(e.target.value);
                        setModelId('');
                      }}
                      placeholder={t('model.placeholder')}
                      className="w-full"
                    />
                  )}
                </div>

                {/* Endpoint */}
                {!isStandardOpenAI && (
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1.5">
                      {t('endpoint.label')}
                    </label>
                    <Input
                      type="url"
                      value={apiEndpoint}
                      onChange={(e) => setApiEndpoint(e.target.value)}
                      placeholder={t('endpoint.placeholder')}
                      className="w-full"
                    />
                  </div>
                )}

                {/* Test Connection */}
                <div className="pt-2">
                  <Button
                    variant="secondary"
                    onClick={handleTestConnection}
                    loading={testing}
                    disabled={testing || !selectedProviderId || !effectiveModel}
                    className="w-full sm:w-auto"
                  >
                    {testing ? t('testing') : t('testConnection')}
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
                        ? t('testSuccess', { latency: testResult.latencyMs ?? 0 })
                        : t('testError', { error: testResult.error ?? 'Unknown error' })}
                    </div>
                  )}
                </div>

                {/* Actions */}
                <div className="flex flex-col sm:flex-row gap-3 pt-3 border-t border-gray-100">
                  <Button
                    variant="primary"
                    onClick={handleSave}
                    loading={saving}
                    disabled={saving || !selectedProviderId || !effectiveModel}
                    className="flex-1 sm:flex-none"
                  >
                    {saving ? t('saving') : t('save')}
                  </Button>

                  {settings && (
                    <Button
                      variant="danger"
                      onClick={() => setShowRemoveConfirm(true)}
                      disabled={removing}
                      className="flex-1 sm:flex-none"
                    >
                      {t('remove')}
                    </Button>
                  )}
                </div>
              </CardContent>
            </Card>
          </div>
        )}
      </main>

      <ConfirmationDialog
        isOpen={showRemoveConfirm}
        onClose={() => setShowRemoveConfirm(false)}
        onConfirm={handleRemove}
        title={t('remove')}
        description={t('removeConfirm')}
        confirmText={tCommon('confirm')}
        cancelText={tCommon('cancel')}
        variant="danger"
      />
    </div>
  );
}

export const dynamic = 'force-dynamic';
