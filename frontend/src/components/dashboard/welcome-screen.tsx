'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { useFeatures } from '@/contexts/features-context';
import { useLocale } from '@/contexts/locale-context';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import { AddAccountButton } from '@/components/buttons/add-account-button';
import { Button } from '@/components/ui/button';
import Link from 'next/link';
import {
  BuildingLibraryIcon,
  TagIcon,
  DocumentTextIcon,
  CheckCircleIcon,
  PlusCircleIcon,
  ArrowDownTrayIcon,
} from '@heroicons/react/24/outline';

interface WelcomeScreenProps {
  accountCount: number;
  hasCategories: boolean;
  onAccountAdded: () => void;
  onCategoriesInitialized: () => void;
}

export function WelcomeScreen({
  accountCount,
  hasCategories,
  onAccountAdded,
  onCategoriesInitialized,
}: WelcomeScreenProps) {
  const t = useTranslations('dashboard.onboarding');
  const router = useRouter();
  const { features } = useFeatures();
  const { locale } = useLocale();
  const [initializingCategories, setInitializingCategories] = useState(false);

  const handleInitializeCategories = async () => {
    setInitializingCategories(true);
    try {
      await apiClient.initializeCategories(locale);
      toast.success(t('initCategories.done'));
      onCategoriesInitialized();
    } catch (error) {
      console.error('Failed to initialize categories:', error);
      toast.error(String(error));
    } finally {
      setInitializingCategories(false);
    }
  };

  const accountDone = accountCount > 0;
  const categoriesDone = hasCategories;

  return (
    <div className="max-w-3xl mx-auto">
      {/* Header */}
      <div className="text-center mb-10">
        <div className="w-20 h-20 bg-gradient-to-br from-primary-500 to-primary-700 rounded-3xl shadow-2xl flex items-center justify-center mx-auto mb-6">
          <BuildingLibraryIcon className="w-10 h-10 text-white" />
        </div>
        <h2 className="text-2xl sm:text-3xl font-bold text-gray-900 mb-2">
          {t('title')}
        </h2>
        <p className="text-base sm:text-lg text-gray-600">
          {t('subtitle')}
        </p>
      </div>

      {/* Action Cards */}
      <div className="space-y-4">
        {/* Card 1: Add an Account */}
        <div className={`bg-white/90 backdrop-blur-xs shadow-lg rounded-2xl p-6 border-0 border-l-4 ${accountDone ? 'border-l-success-500' : 'border-l-primary-500'} animate-fade-in-up`}>
          <div className="flex items-start gap-4">
            <div className={`flex-shrink-0 w-8 h-8 rounded-full flex items-center justify-center text-sm font-bold ${accountDone ? 'bg-success-100 text-success-700' : 'bg-primary-100 text-primary-700'}`}>
              {accountDone ? <CheckCircleIcon className="w-5 h-5" /> : '1'}
            </div>
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 mb-1">
                <BuildingLibraryIcon className="w-5 h-5 text-gray-500" />
                <h3 className="text-lg font-semibold text-gray-900">{t('addAccount.title')}</h3>
              </div>
              <p className="text-sm text-gray-600 mb-3">{t('addAccount.description')}</p>
              {accountDone ? (
                <p className="text-sm font-medium text-success-600">{t('addAccount.done')}</p>
              ) : (
                <AddAccountButton
                  onSuccess={onAccountAdded}
                  className="bg-gradient-to-r from-primary-500 to-primary-700 hover:from-primary-600 hover:to-primary-800 text-white rounded-lg"
                />
              )}
            </div>
          </div>
        </div>

        {/* Card 2: Initialize Categories */}
        <div className={`bg-white/90 backdrop-blur-xs shadow-lg rounded-2xl p-6 border-0 border-l-4 ${categoriesDone ? 'border-l-success-500' : 'border-l-primary-500'} animate-fade-in-up`} style={{ animationDelay: '0.1s' }}>
          <div className="flex items-start gap-4">
            <div className={`flex-shrink-0 w-8 h-8 rounded-full flex items-center justify-center text-sm font-bold ${categoriesDone ? 'bg-success-100 text-success-700' : 'bg-primary-100 text-primary-700'}`}>
              {categoriesDone ? <CheckCircleIcon className="w-5 h-5" /> : '2'}
            </div>
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 mb-1">
                <TagIcon className="w-5 h-5 text-gray-500" />
                <h3 className="text-lg font-semibold text-gray-900">{t('initCategories.title')}</h3>
              </div>
              <p className="text-sm text-gray-600 mb-3">{t('initCategories.description')}</p>
              {categoriesDone ? (
                <p className="text-sm font-medium text-success-600">{t('initCategories.done')}</p>
              ) : (
                <>
                  <Button
                    onClick={handleInitializeCategories}
                    disabled={initializingCategories}
                    className="bg-gradient-to-r from-primary-500 to-primary-700 hover:from-primary-600 hover:to-primary-800 text-white rounded-lg"
                  >
                    <TagIcon className="w-5 h-5 mr-2" />
                    {initializingCategories ? t('initCategories.loading') : t('initCategories.button')}
                  </Button>
                  <p className="text-sm text-gray-500 mt-2">
                    {t('initCategories.orCustom.text')}{' '}
                    <Link href="/categories/new" className="text-primary-600 hover:text-primary-800 underline">
                      {t('initCategories.orCustom.link')}
                    </Link>
                  </p>
                </>
              )}
            </div>
          </div>
        </div>

        {/* Card 3: Add Transactions */}
        <div className="bg-white/90 backdrop-blur-xs shadow-lg rounded-2xl p-6 border-0 border-l-4 border-l-primary-500 animate-fade-in-up" style={{ animationDelay: '0.2s' }}>
          <div className="flex items-start gap-4">
            <div className="flex-shrink-0 w-8 h-8 rounded-full flex items-center justify-center text-sm font-bold bg-primary-100 text-primary-700">
              {'3'}
            </div>
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 mb-1">
                <DocumentTextIcon className="w-5 h-5 text-gray-500" />
                <h3 className="text-lg font-semibold text-gray-900">{t('addTransactions.title')}</h3>
              </div>
              <p className="text-sm text-gray-600 mb-3">{t('addTransactions.description')}</p>
              <div className="flex flex-col sm:flex-row gap-2">
                <Button
                  onClick={() => router.push('/transactions/new')}
                  className="bg-gradient-to-r from-primary-500 to-primary-700 hover:from-primary-600 hover:to-primary-800 text-white rounded-lg"
                >
                  <PlusCircleIcon className="w-5 h-5 mr-2" />
                  {t('addTransactions.manual')}
                </Button>
                <Button
                  onClick={() => router.push(features.aiCategorization ? '/import/ai-csv' : '/import')}
                  variant="outline"
                  className="border-primary-300 text-primary-700 hover:bg-primary-50 rounded-lg"
                >
                  <ArrowDownTrayIcon className="w-5 h-5 mr-2" />
                  {t('addTransactions.import')}
                </Button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
