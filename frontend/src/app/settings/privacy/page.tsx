'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import Link from 'next/link';
import {
  ShieldCheckIcon,
  ArrowLeftIcon,
  ArrowDownTrayIcon,
  TrashIcon,
  ExclamationTriangleIcon,
} from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';
import { apiClient, UserDataSummary } from '@/lib/api-client';
import { toast } from 'sonner';

export default function PrivacySettingsPage() {
  const { isAuthenticated, isLoading, logout } = useAuth();
  const router = useRouter();
  const t = useTranslations('settings.privacy');
  const tCommon = useTranslations('common');

  const [summary, setSummary] = useState<UserDataSummary | null>(null);
  const [loadingSummary, setLoadingSummary] = useState(true);
  const [exporting, setExporting] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteConfirmText, setDeleteConfirmText] = useState('');
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  useEffect(() => {
    if (isAuthenticated) {
      loadSummary();
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated]);

  const loadSummary = async () => {
    try {
      setLoadingSummary(true);
      const data = await apiClient.getUserDataSummary();
      setSummary(data);
    } catch (error) {
      console.error('Failed to load data summary:', error);
      toast.error(t('errors.loadSummaryFailed'));
    } finally {
      setLoadingSummary(false);
    }
  };

  const handleExportData = async () => {
    try {
      setExporting(true);
      const blob = await apiClient.exportUserData();

      // Create download link
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `mymascada-data-export-${new Date().toISOString().split('T')[0]}.json`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);

      toast.success(t('export.success'));
    } catch (error) {
      console.error('Failed to export data:', error);
      toast.error(t('errors.exportFailed'));
    } finally {
      setExporting(false);
    }
  };

  const handleDeleteAccount = async () => {
    if (deleteConfirmText !== 'DELETE') {
      toast.error(t('delete.confirmError'));
      return;
    }

    try {
      setDeleting(true);
      await apiClient.deleteUserAccount('DELETE');
      toast.success(t('delete.success'));

      // Log out and redirect to login
      await logout();
      router.push('/auth/login');
    } catch (error) {
      console.error('Failed to delete account:', error);
      toast.error(t('errors.deleteFailed'));
    } finally {
      setDeleting(false);
      setShowDeleteConfirm(false);
      setDeleteConfirmText('');
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <ShieldCheckIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-slate-700 font-medium">{tCommon('loading')}</div>
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
          <Link href="/settings" className="inline-flex items-center gap-2 text-sm text-violet-600 hover:text-violet-800 mb-4">
            <ArrowLeftIcon className="w-4 h-4" />
            {t('backToSettings')}
          </Link>
          <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900 sm:text-[2.1rem]">
            {t('title')}
          </h1>
          <p className="text-[15px] text-slate-500 mt-1.5">
            {t('subtitle')}
          </p>
        </div>

        <div className="space-y-6">
          {/* Data Summary Card */}
          <Card className="rounded-[26px] border border-violet-100/70 bg-white/92 shadow-[0_20px_46px_-30px_rgba(76,29,149,0.45)] backdrop-blur-xs">
            <CardContent className="p-6">
              <h2 className="text-lg font-semibold text-slate-900 mb-4">{t('dataSummary.title')}</h2>

              {loadingSummary ? (
                <div className="animate-pulse space-y-2">
                  <div className="h-4 bg-slate-200 rounded w-3/4"></div>
                  <div className="h-4 bg-slate-200 rounded w-1/2"></div>
                  <div className="h-4 bg-slate-200 rounded w-2/3"></div>
                </div>
              ) : summary ? (
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                  <div className="text-center p-3 bg-violet-50/40 rounded-xl border border-violet-100/60">
                    <div className="text-2xl font-bold text-violet-600">{summary.totalAccounts}</div>
                    <div className="text-sm text-slate-600">{t('dataSummary.accounts')}</div>
                  </div>
                  <div className="text-center p-3 bg-violet-50/40 rounded-xl border border-violet-100/60">
                    <div className="text-2xl font-bold text-violet-600">{summary.totalTransactions}</div>
                    <div className="text-sm text-slate-600">{t('dataSummary.transactions')}</div>
                  </div>
                  <div className="text-center p-3 bg-violet-50/40 rounded-xl border border-violet-100/60">
                    <div className="text-2xl font-bold text-violet-600">{summary.totalCategories}</div>
                    <div className="text-sm text-slate-600">{t('dataSummary.categories')}</div>
                  </div>
                  <div className="text-center p-3 bg-violet-50/40 rounded-xl border border-violet-100/60">
                    <div className="text-2xl font-bold text-violet-600">{summary.totalRules}</div>
                    <div className="text-sm text-slate-600">{t('dataSummary.rules')}</div>
                  </div>
                </div>
              ) : (
                <p className="text-slate-500">{t('dataSummary.unavailable')}</p>
              )}
            </CardContent>
          </Card>

          {/* Export Data Card */}
          <Card className="rounded-[26px] border border-violet-100/70 bg-white/92 shadow-[0_20px_46px_-30px_rgba(76,29,149,0.45)] backdrop-blur-xs">
            <CardContent className="p-6">
              <div className="flex items-start gap-4">
                <div className="w-12 h-12 bg-gradient-to-br from-blue-400 to-blue-600 rounded-xl flex items-center justify-center shrink-0">
                  <ArrowDownTrayIcon className="w-6 h-6 text-white" />
                </div>
                <div className="flex-1">
                  <h2 className="text-lg font-semibold text-slate-900">{t('export.title')}</h2>
                  <p className="text-sm text-slate-500 mt-1 mb-4">
                    {t('export.description')}
                  </p>
                  <Button
                    onClick={handleExportData}
                    disabled={exporting}
                    className="bg-blue-600 hover:bg-blue-700"
                  >
                    {exporting ? t('export.exporting') : t('export.button')}
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Delete Account Card */}
          <Card className="rounded-[26px] border border-violet-100/70 bg-white/92 shadow-[0_20px_46px_-30px_rgba(76,29,149,0.45)] backdrop-blur-xs border-l-4 border-l-red-500">
            <CardContent className="p-6">
              <div className="flex items-start gap-4">
                <div className="w-12 h-12 bg-gradient-to-br from-red-400 to-red-600 rounded-xl flex items-center justify-center shrink-0">
                  <TrashIcon className="w-6 h-6 text-white" />
                </div>
                <div className="flex-1">
                  <h2 className="text-lg font-semibold text-slate-900">{t('delete.title')}</h2>
                  <p className="text-sm text-slate-500 mt-1 mb-4">
                    {t('delete.description')}
                  </p>

                  {!showDeleteConfirm ? (
                    <Button
                      onClick={() => setShowDeleteConfirm(true)}
                      variant="danger"
                      className="bg-red-600 hover:bg-red-700"
                    >
                      {t('delete.button')}
                    </Button>
                  ) : (
                    <div className="bg-red-50 border border-red-200 rounded-lg p-4 space-y-4">
                      <div className="flex items-start gap-3">
                        <ExclamationTriangleIcon className="w-6 h-6 text-red-600 shrink-0 mt-0.5" />
                        <div>
                          <h3 className="font-semibold text-red-800">{t('delete.warning.title')}</h3>
                          <p className="text-sm text-red-700 mt-1">{t('delete.warning.message')}</p>
                          <ul className="text-sm text-red-700 mt-2 list-disc list-inside">
                            <li>{t('delete.warning.item1')}</li>
                            <li>{t('delete.warning.item2')}</li>
                            <li>{t('delete.warning.item3')}</li>
                          </ul>
                        </div>
                      </div>

                      <div>
                        <label className="block text-sm font-medium text-red-800 mb-2">
                          {t('delete.confirmLabel')}
                        </label>
                        <input
                          type="text"
                          value={deleteConfirmText}
                          onChange={(e) => setDeleteConfirmText(e.target.value)}
                          placeholder="DELETE"
                          className="w-full px-3 py-2 border border-red-300 rounded-lg focus:ring-2 focus:ring-red-500 focus:border-red-500"
                        />
                      </div>

                      <div className="flex gap-3">
                        <Button
                          onClick={handleDeleteAccount}
                          disabled={deleting || deleteConfirmText !== 'DELETE'}
                          variant="danger"
                          className="bg-red-600 hover:bg-red-700 disabled:opacity-50"
                        >
                          {deleting ? t('delete.deleting') : t('delete.confirmButton')}
                        </Button>
                        <Button
                          onClick={() => {
                            setShowDeleteConfirm(false);
                            setDeleteConfirmText('');
                          }}
                          variant="outline"
                        >
                          {tCommon('cancel')}
                        </Button>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
    </AppLayout>
  );
}

export const dynamic = 'force-dynamic';
