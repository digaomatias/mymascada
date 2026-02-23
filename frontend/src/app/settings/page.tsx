'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { useCallback, useEffect, useState } from 'react';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent } from '@/components/ui/card';
import { Select } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import Link from 'next/link';
import {
  CogIcon,
  BuildingLibraryIcon,
  UserIcon,
  BellIcon,
  ShieldCheckIcon,
  ChevronRightIcon,
  LanguageIcon,
  TagIcon,
  ArrowDownTrayIcon,
  SparklesIcon,
  ChatBubbleBottomCenterTextIcon,
  PresentationChartBarIcon
} from '@heroicons/react/24/outline';
import { useLocale } from '@/contexts/locale-context';
import { useTranslations } from 'next-intl';
import { apiClient } from '@/lib/api-client';
import { useFeatures } from '@/contexts/features-context';

interface SettingsItem {
  href: string;
  icon: React.ComponentType<{ className?: string }>;
  labelKey: string;
  badge?: boolean;
}

const settingsItems: SettingsItem[] = [
  {
    href: '/settings/bank-connections',
    icon: BuildingLibraryIcon,
    labelKey: 'bankConnections',
    badge: true
  },
  {
    href: '/settings/ai',
    icon: SparklesIcon,
    labelKey: 'aiSettings',
  },
  {
    href: '/settings/telegram',
    icon: ChatBubbleBottomCenterTextIcon,
    labelKey: 'telegram',
  },
  {
    href: '/settings/privacy',
    icon: ShieldCheckIcon,
    labelKey: 'privacy',
  },
];

export default function SettingsPage() {
  const { isAuthenticated, isLoading, refreshUser, user } = useAuth();
  const router = useRouter();
  const { locale, setLocale, locales, localeNames, isLoading: localeLoading } = useLocale();
  const { features } = useFeatures();
  const t = useTranslations('settings');
  const tCommon = useTranslations('common');
  const [isSavingLocale, setIsSavingLocale] = useState(false);
  const [aiCleaningEnabled, setAiCleaningEnabled] = useState(false);
  const [isSavingAiCleaning, setIsSavingAiCleaning] = useState(false);
  const [dashboardTemplate, setDashboardTemplate] = useState<'education' | 'advanced'>('education');

  useEffect(() => {
    try {
      // Check for new key first
      const stored = localStorage.getItem('mymascada_dashboard_template');
      if (stored === 'education' || stored === 'advanced') {
        setDashboardTemplate(stored);
        return;
      }
      // Migrate old key if present
      const oldValue = localStorage.getItem('mymascada_dashboard_layout');
      if (oldValue) {
        const migrated = oldValue === 'classic' ? 'advanced' : 'education';
        setDashboardTemplate(migrated as 'education' | 'advanced');
        localStorage.setItem('mymascada_dashboard_template', migrated);
        localStorage.removeItem('mymascada_dashboard_layout');
      }
    } catch {
      // Ignore localStorage errors
    }
  }, []);

  const handleDashboardTemplateToggle = () => {
    const newTemplate = dashboardTemplate === 'education' ? 'advanced' : 'education';
    setDashboardTemplate(newTemplate);
    try {
      localStorage.setItem('mymascada_dashboard_template', newTemplate);
    } catch {
      // Ignore localStorage errors
    }
  };

  // Category seeding state
  const [seedLocales, setSeedLocales] = useState<string[]>([]);
  const [selectedSeedLocale, setSelectedSeedLocale] = useState('en');
  const [categoryCount, setCategoryCount] = useState<number | null>(null);
  const [isLoadingCategories, setIsLoadingCategories] = useState(true);
  const [isSeeding, setIsSeeding] = useState(false);
  const [seedResult, setSeedResult] = useState<{ success: boolean; count?: number; error?: string } | null>(null);

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  useEffect(() => {
    if (user?.aiDescriptionCleaning !== undefined) {
      setAiCleaningEnabled(user.aiDescriptionCleaning);
    }
  }, [user?.aiDescriptionCleaning]);

  const handleAiCleaningToggle = async () => {
    const newValue = !aiCleaningEnabled;
    setAiCleaningEnabled(newValue);
    setIsSavingAiCleaning(true);
    try {
      await apiClient.updateAiDescriptionCleaning(newValue);
      await refreshUser();
    } catch (error) {
      console.error('Failed to update AI description cleaning:', error);
      setAiCleaningEnabled(!newValue);
    } finally {
      setIsSavingAiCleaning(false);
    }
  };

  const fetchCategorySeedingData = useCallback(async () => {
    setIsLoadingCategories(true);
    try {
      const [localesResponse, categoriesResponse] = await Promise.all([
        apiClient.getSeedLocales().catch(() => ['en', 'pt-BR']),
        apiClient.getCategories() as Promise<unknown[]>,
      ]);
      setSeedLocales(localesResponse);
      setCategoryCount(Array.isArray(categoriesResponse) ? categoriesResponse.length : 0);
    } catch (error) {
      console.error('Failed to load category seeding data:', error);
      setCategoryCount(0);
    } finally {
      setIsLoadingCategories(false);
    }
  }, []);

  useEffect(() => {
    if (isAuthenticated && !isLoading) {
      fetchCategorySeedingData();
    }
  }, [isAuthenticated, isLoading, fetchCategorySeedingData]);

  const handleSeedCategories = async () => {
    setIsSeeding(true);
    setSeedResult(null);
    try {
      const result = await apiClient.initializeCategories(selectedSeedLocale);
      setSeedResult({ success: true, count: result.count });
      // Refresh category count after seeding
      const categoriesResponse = await apiClient.getCategories() as unknown[];
      setCategoryCount(Array.isArray(categoriesResponse) ? categoriesResponse.length : 0);
    } catch (error) {
      console.error('Failed to seed categories:', error);
      setSeedResult({ success: false, error: (error as Error).message });
    } finally {
      setIsSeeding(false);
    }
  };

  const handleLocaleChange = async (newLocale: string) => {
    if (newLocale === locale) return;

    setIsSavingLocale(true);
    try {
      await apiClient.updateLocale(newLocale);
      await refreshUser();
      setLocale(newLocale as typeof locale);
    } catch (error) {
      console.error('Failed to update locale:', error);
    } finally {
      setIsSavingLocale(false);
    }
  };

  if (isLoading || localeLoading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <CogIcon className="w-8 h-8 text-white" />
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
        {/* Header */}
        <div className="mb-6 lg:mb-8">
          <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900">
            {t('title')}
          </h1>
          <p className="text-gray-600 mt-1">
            {t('subtitle')}
          </p>
        </div>

        {/* Settings Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {/* Language Preference Card */}
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg h-full">
            <CardContent className="p-6">
              <div className="flex items-start gap-4">
                <div className="w-12 h-12 bg-gradient-to-br from-primary-400 to-primary-600 rounded-xl flex items-center justify-center shrink-0">
                  <LanguageIcon className="w-6 h-6 text-white" />
                </div>
                <div className="flex-1 min-w-0">
                  <h3 className="text-lg font-semibold text-gray-900">
                    {t('language.title')}
                  </h3>
                  <p className="text-sm text-gray-600 mt-1 mb-3">
                    {t('language.description')}
                  </p>
                  <Select
                    value={locale}
                    onChange={(e) => handleLocaleChange(e.target.value)}
                    disabled={isSavingLocale}
                    className="w-full"
                  >
                    {locales.map((loc) => (
                      <option key={loc} value={loc}>
                        {localeNames[loc]}
                      </option>
                    ))}
                  </Select>
                  {isSavingLocale && (
                    <p className="text-sm text-primary-600 mt-2">
                      {t('language.saving')}
                    </p>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Default Categories Card */}
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg h-full">
            <CardContent className="p-6">
              <div className="flex items-start gap-4">
                <div className="w-12 h-12 bg-gradient-to-br from-primary-400 to-primary-600 rounded-xl flex items-center justify-center shrink-0">
                  <TagIcon className="w-6 h-6 text-white" />
                </div>
                <div className="flex-1 min-w-0">
                  <h3 className="text-lg font-semibold text-gray-900">
                    {t('categorySeeding.title')}
                  </h3>
                  <p className="text-sm text-gray-600 mt-1 mb-3">
                    {t('categorySeeding.description')}
                  </p>

                  {isLoadingCategories ? (
                    <p className="text-sm text-gray-500">{tCommon('loading')}</p>
                  ) : categoryCount !== null && categoryCount > 0 ? (
                    <div>
                      <p className="text-sm text-amber-700 bg-amber-50 border border-amber-200 rounded-lg p-3 mb-3">
                        {t('categorySeeding.alreadyHasCategories')}
                      </p>
                      <p className="text-sm text-gray-500">
                        {t('categorySeeding.hasCategories', { count: categoryCount })}
                      </p>
                    </div>
                  ) : (
                    <div>
                      <p className="text-sm text-blue-700 bg-blue-50 border border-blue-200 rounded-lg p-3 mb-3">
                        {t('categorySeeding.noCategories')}
                      </p>
                      <label className="block text-sm font-medium text-gray-700 mb-1">
                        {t('categorySeeding.localeLabel')}
                      </label>
                      <Select
                        value={selectedSeedLocale}
                        onChange={(e) => setSelectedSeedLocale(e.target.value)}
                        disabled={isSeeding}
                        className="w-full mb-3"
                      >
                        {seedLocales.map((loc) => (
                          <option key={loc} value={loc}>
                            {loc === 'en' ? t('categorySeeding.localeEn') : loc === 'pt-BR' ? t('categorySeeding.localePtBR') : loc}
                          </option>
                        ))}
                      </Select>
                      <Button
                        onClick={handleSeedCategories}
                        loading={isSeeding}
                        disabled={isSeeding}
                        variant="primary"
                        size="md"
                        className="w-full"
                      >
                        {isSeeding ? t('categorySeeding.seeding') : t('categorySeeding.seedButton')}
                      </Button>
                    </div>
                  )}

                  {seedResult && seedResult.success && (
                    <p className="text-sm text-green-700 bg-green-50 border border-green-200 rounded-lg p-3 mt-3">
                      {t('categorySeeding.success', { count: seedResult.count ?? 0 })}
                    </p>
                  )}
                  {seedResult && !seedResult.success && (
                    <p className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-lg p-3 mt-3">
                      {t('categorySeeding.error')}
                    </p>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Transaction Import Card - only visible when AI Categorization is enabled */}
          {features.aiCategorization && (
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg h-full">
              <CardContent className="p-6">
                <div className="flex items-start gap-4">
                  <div className="w-12 h-12 bg-gradient-to-br from-primary-400 to-primary-600 rounded-xl flex items-center justify-center shrink-0">
                    <ArrowDownTrayIcon className="w-6 h-6 text-white" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <h3 className="text-lg font-semibold text-gray-900">
                      {t('transactionImport.title')}
                    </h3>
                    <p className="text-sm text-gray-600 mt-1 mb-3">
                      {t('transactionImport.description')}
                    </p>
                    <div className="flex items-center justify-between">
                      <div>
                        <p className="text-sm font-medium text-gray-900">
                          {t('transactionImport.aiCleaning')}
                        </p>
                        <p className="text-xs text-gray-500 mt-0.5">
                          {t('transactionImport.aiCleaningDescription')}
                        </p>
                      </div>
                      <button
                        type="button"
                        role="switch"
                        aria-checked={aiCleaningEnabled}
                        onClick={handleAiCleaningToggle}
                        disabled={isSavingAiCleaning}
                        className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-primary-500 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed ${
                          aiCleaningEnabled ? 'bg-primary-600' : 'bg-gray-200'
                        }`}
                      >
                        <span
                          className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${
                            aiCleaningEnabled ? 'translate-x-5' : 'translate-x-0'
                          }`}
                        />
                      </button>
                    </div>
                  </div>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Dashboard Template Card */}
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg h-full">
            <CardContent className="p-6">
              <div className="flex items-start gap-4">
                <div className="w-12 h-12 bg-gradient-to-br from-primary-400 to-primary-600 rounded-xl flex items-center justify-center shrink-0">
                  <PresentationChartBarIcon className="w-6 h-6 text-white" />
                </div>
                <div className="flex-1 min-w-0">
                  <h3 className="text-lg font-semibold text-gray-900">
                    {t('dashboardLayout.title')}
                  </h3>
                  <p className="text-sm text-gray-600 mt-1 mb-3">
                    {t('dashboardLayout.description')}
                  </p>
                  <div className="flex items-center justify-between">
                    <div>
                      <p className="text-sm font-medium text-gray-900">
                        {dashboardTemplate === 'education'
                          ? t('dashboardLayout.education')
                          : t('dashboardLayout.advanced')}
                      </p>
                    </div>
                    <button
                      type="button"
                      role="switch"
                      aria-checked={dashboardTemplate === 'education'}
                      onClick={handleDashboardTemplateToggle}
                      className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-primary-500 focus:ring-offset-2 ${
                        dashboardTemplate === 'education' ? 'bg-primary-600' : 'bg-gray-200'
                      }`}
                    >
                      <span
                        className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${
                          dashboardTemplate === 'education' ? 'translate-x-5' : 'translate-x-0'
                        }`}
                      />
                    </button>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Other Settings Items */}
          {settingsItems.map((item) => {
            const IconComponent = item.icon;
            return (
              <Link key={item.href} href={item.href}>
                <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg hover:shadow-xl transition-shadow cursor-pointer h-full">
                  <CardContent className="p-6">
                    <div className="flex items-start gap-4">
                      <div className="w-12 h-12 bg-gradient-to-br from-primary-400 to-primary-600 rounded-xl flex items-center justify-center shrink-0">
                        <IconComponent className="w-6 h-6 text-white" />
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2">
                          <h3 className="text-lg font-semibold text-gray-900">
                            {t(`items.${item.labelKey}.title`)}
                          </h3>
                          {item.badge && (
                            <span className="px-2 py-0.5 text-xs font-medium bg-blue-100 text-blue-700 rounded-full">
                              {t('badges.new')}
                            </span>
                          )}
                        </div>
                        <p className="text-sm text-gray-600 mt-1">
                          {t(`items.${item.labelKey}.description`)}
                        </p>
                      </div>
                      <ChevronRightIcon className="w-5 h-5 text-gray-400 shrink-0" />
                    </div>
                  </CardContent>
                </Card>
              </Link>
            );
          })}
        </div>

        {/* Coming Soon Section */}
        <div className="mt-8">
          <h2 className="text-lg font-semibold text-gray-700 mb-4">{t('comingSoon.title')}</h2>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            {[
              { icon: UserIcon, labelKey: 'profile' as const },
              { icon: BellIcon, labelKey: 'notifications' as const },
              { icon: ShieldCheckIcon, labelKey: 'security' as const },
            ].map((item) => {
              const IconComponent = item.icon;
              return (
                <Card key={item.labelKey} className="bg-white/50 border border-gray-200">
                  <CardContent className="p-4">
                    <div className="flex items-center gap-3 opacity-50">
                      <div className="w-10 h-10 bg-gray-300 rounded-lg flex items-center justify-center">
                        <IconComponent className="w-5 h-5 text-gray-500" />
                      </div>
                      <div>
                        <h3 className="font-medium text-gray-700">{t(`comingSoon.items.${item.labelKey}.title`)}</h3>
                        <p className="text-xs text-gray-500">{t(`comingSoon.items.${item.labelKey}.description`)}</p>
                      </div>
                    </div>
                  </CardContent>
                </Card>
              );
            })}
          </div>
        </div>
    </AppLayout>
  );
}

export const dynamic = 'force-dynamic';
