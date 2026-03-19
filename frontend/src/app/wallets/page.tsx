'use client';

import { useEffect, useState, useCallback } from 'react';
import { useTranslations } from 'next-intl';
import { useRouter } from 'next/navigation';
import { AppLayout } from '@/components/app-layout';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { BaseModal } from '@/components/modals/base-modal';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  apiClient,
  WalletSummary,
  CreateWalletRequest,
  UpdateWalletRequest,
} from '@/lib/api-client';
import { formatCurrency, cn } from '@/lib/utils';
import { WALLET_ICONS, WalletIcon, DEFAULT_WALLET_ICON_ID } from '@/lib/wallet-icons';
import { toast } from 'sonner';
import {
  ArrowRightIcon,
  CircleStackIcon,
  PlusIcon,
  PencilIcon,
  TrashIcon,
} from '@heroicons/react/24/outline';
import { WalletsSkeleton } from '@/components/skeletons';
import { useAuthGuard } from '@/hooks/use-auth-guard';

const WALLET_COLORS = [
  '#7c3aed', '#2563eb', '#059669', '#d97706',
  '#dc2626', '#db2777', '#0891b2', '#4f46e5',
  '#65a30d', '#ea580c', '#6d28d9', '#0d9488',
];

const CURRENCIES = [
  'NZD', 'USD', 'EUR', 'BRL', 'GBP', 'AUD',
  'CAD', 'JPY', 'ARS', 'CLP', 'COP', 'MXN',
];

const DEFAULT_COLOR = '#7c3aed';

interface WalletFormData {
  name: string;
  icon: string;
  color: string;
  currency: string;
  targetAmount: string;
}

const defaultFormData: WalletFormData = {
  name: '',
  icon: DEFAULT_WALLET_ICON_ID,
  color: DEFAULT_COLOR,
  currency: 'NZD',
  targetAmount: '',
};

export default function WalletsPage() {
  const { shouldRender, isAuthResolved } = useAuthGuard();
  const t = useTranslations('wallets');
  const tCommon = useTranslations('common');
  const router = useRouter();
  const [wallets, setWallets] = useState<WalletSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [hasLoadError, setHasLoadError] = useState(false);
  const [showArchived, setShowArchived] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const [editingWallet, setEditingWallet] = useState<WalletSummary | null>(null);
  const [formData, setFormData] = useState<WalletFormData>(defaultFormData);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [walletToDelete, setWalletToDelete] = useState<WalletSummary | null>(null);

  const loadWallets = useCallback(async () => {
    try {
      setHasLoadError(false);
      setIsLoading(true);
      const data = await apiClient.getWallets({ includeArchived: showArchived });
      setWallets(data);
    } catch {
      setHasLoadError(true);
      toast.error(t('loadError'));
    } finally {
      setIsLoading(false);
    }
  }, [showArchived, t]);

  useEffect(() => {
    if (!isAuthResolved) return;
    loadWallets();
  }, [isAuthResolved, loadWallets]);

  // Fix #2: Group totals by currency instead of summing across currencies
  const balanceByCurrency = wallets
    .filter((w) => !w.isArchived)
    .reduce((acc, w) => {
      acc[w.currency] = (acc[w.currency] || 0) + w.balance;
      return acc;
    }, {} as Record<string, number>);

  const currencyTotals = Object.entries(balanceByCurrency);

  const openCreateModal = () => {
    setEditingWallet(null);
    setFormData(defaultFormData);
    setShowModal(true);
  };

  const openEditModal = (wallet: WalletSummary, e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setEditingWallet(wallet);
    setFormData({
      name: wallet.name,
      icon: wallet.icon || DEFAULT_WALLET_ICON_ID,
      color: wallet.color || DEFAULT_COLOR,
      currency: wallet.currency,
      targetAmount: wallet.targetAmount?.toString() ?? '',
    });
    setShowModal(true);
  };

  const handleSubmit = async () => {
    if (!formData.name.trim()) return;

    setIsSubmitting(true);
    try {
      if (editingWallet) {
        // Fix #10: When target amount is cleared, set clearTargetAmount: true
        const hadTarget = editingWallet.targetAmount != null && editingWallet.targetAmount > 0;
        const rawTarget = formData.targetAmount?.trim();
        const parsedTarget = rawTarget ? Number(rawTarget) : null;
        const newTarget = parsedTarget !== null && Number.isFinite(parsedTarget) && parsedTarget >= 0 ? parsedTarget : null;
        const update: UpdateWalletRequest = {
          name: formData.name.trim(),
          icon: formData.icon,
          color: formData.color,
          currency: formData.currency,
          targetAmount: newTarget,
          clearTargetAmount: hadTarget && !newTarget ? true : undefined,
        };
        await apiClient.updateWallet(editingWallet.id, update);
        toast.success(t('walletUpdated'));
      } else {
        const create: CreateWalletRequest = {
          name: formData.name.trim(),
          icon: formData.icon,
          color: formData.color,
          currency: formData.currency,
          targetAmount: (() => {
            const raw = formData.targetAmount?.trim();
            const parsed = raw ? Number(raw) : undefined;
            return parsed !== undefined && Number.isFinite(parsed) && parsed >= 0 ? parsed : undefined;
          })(),
        };
        await apiClient.createWallet(create);
        toast.success(t('walletCreated'));
      }
      setShowModal(false);
      loadWallets();
    } catch {
      toast.error(editingWallet ? t('updateError') : t('createError'));
    } finally {
      setIsSubmitting(false);
    }
  };

  // Fix #1: Replace window.confirm with ConfirmationDialog
  const confirmDeleteWallet = (wallet: WalletSummary, e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setWalletToDelete(wallet);
  };

  const handleDelete = async () => {
    if (!walletToDelete) return;

    try {
      await apiClient.deleteWallet(walletToDelete.id);
      toast.success(t('walletDeleted'));
      loadWallets();
    } catch {
      toast.error(t('deleteError'));
    } finally {
      setWalletToDelete(null);
    }
  };

  if (!shouldRender) return null;

  return (
    <AppLayout>
      {/* Header */}
      <header className="mb-5">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-ink-900 sm:text-[2.1rem]">
              {t('title')}
            </h1>
            <p className="mt-1.5 text-[15px] text-ink-500">
              {t('subtitle')}
            </p>
          </div>
          <Button onClick={openCreateModal}>
            <PlusIcon className="mr-1.5 h-4 w-4" />
            <span className="hidden sm:inline">{t('createWallet')}</span>
            <span className="sm:hidden">{tCommon('add')}</span>
          </Button>
        </div>
        <div className="mt-3 flex items-center gap-2">
          <Checkbox
            id="showArchived"
            checked={showArchived}
            onCheckedChange={(checked) => setShowArchived(checked === true)}
          />
          <Label htmlFor="showArchived" className="text-sm text-ink-600">
            {t('showArchived')}
          </Label>
        </div>
      </header>

      <div className="space-y-5">
        {isLoading ? (
          <WalletsSkeleton />
        ) : hasLoadError ? (
          <section className="rounded-[28px] border border-red-100/80 bg-white/92 p-10 text-center shadow-[0_20px_50px_-30px_rgba(47,129,112,0.20)]">
            <p className="text-red-600">{t('loadError')}</p>
            <button
              onClick={loadWallets}
              className="mt-4 text-sm font-semibold text-primary-600 hover:underline"
            >
              {tCommon('retry')}
            </button>
          </section>
        ) : wallets.length === 0 ? (
          /* Empty state */
          <section className="rounded-[28px] border border-ink-200 bg-white/92 p-10 text-center shadow-[0_20px_50px_-30px_rgba(47,129,112,0.20)]">
            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-primary-100 text-primary-600">
              <CircleStackIcon className="h-7 w-7" />
            </div>
            <h3 className="mt-4 text-xl font-semibold text-ink-900">{t('noWallets')}</h3>
            <p className="mt-2 text-sm text-ink-500">
              {t('noWalletsDescription')}
            </p>
            <Button onClick={openCreateModal} className="mt-5">
              <PlusIcon className="mr-1.5 h-4 w-4" />
              {t('createFirstWallet')}
            </Button>
          </section>
        ) : (
          <>
            {/* Fix #2: Total allocated hero — per-currency totals */}
            <section className="rounded-[26px] border border-ink-200 bg-white/90 p-6 shadow-lg shadow-primary-200/20 backdrop-blur-xs">
              <p className="text-xs font-semibold uppercase tracking-[0.12em] text-ink-400">
                {t('totalAllocated')}
              </p>
              <div className="mt-1 flex flex-wrap items-baseline gap-x-4 gap-y-1">
                {currencyTotals.map(([currency, total]) => (
                  <p
                    key={currency}
                    className="font-[var(--font-dash-sans)] text-3xl font-bold tracking-tight text-ink-900"
                  >
                    {formatCurrency(total, currency)}
                  </p>
                ))}
              </div>
            </section>

            {/* Wallet cards grid */}
            {/* Fix #3: Use div instead of Link to avoid nested interactive elements */}
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {wallets.map((wallet) => {
                const color = wallet.color || DEFAULT_COLOR;

                return (
                  <div
                    key={wallet.id}
                    role="button"
                    tabIndex={0}
                    onClick={() => router.push(`/wallets/${wallet.id}`)}
                    onKeyDown={(e) => {
                      if (e.currentTarget !== e.target) return;
                      if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        router.push(`/wallets/${wallet.id}`);
                      }
                    }}
                    className={cn(
                      'group relative cursor-pointer rounded-[26px] border border-ink-200 bg-white/90 p-5 shadow-[0_20px_44px_-32px_rgba(47,129,112,0.20)] transition-shadow hover:shadow-[0_24px_52px_-28px_rgba(47,129,112,0.25)]',
                      wallet.isArchived && 'opacity-60',
                    )}
                  >
                    {/* Color accent bar */}
                    <div
                      className="absolute left-0 top-4 bottom-4 w-1 rounded-r-full"
                      style={{ backgroundColor: color }}
                    />

                    {/* Top row: icon + name + actions */}
                    <div className="flex items-start gap-3 pl-2">
                      <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-primary-50 text-primary-600">
                        <WalletIcon iconId={wallet.icon} className="h-5 w-5" />
                      </span>
                      <div className="min-w-0 flex-1">
                        <div className="flex items-center gap-2">
                          <h3 className="line-clamp-1 text-lg font-semibold tracking-[-0.02em] text-ink-900">
                            {wallet.name}
                          </h3>
                          {wallet.isArchived && (
                            <span className="shrink-0 rounded-full border border-ink-200 bg-ink-50 px-2 py-0.5 text-[11px] font-semibold uppercase tracking-[0.08em] text-ink-500">
                              {t('archived')}
                            </span>
                          )}
                        </div>
                        <p className="text-xs text-ink-500">
                          {t('allocationCount', { count: wallet.allocationCount })}
                        </p>
                      </div>

                      {/* Actions */}
                      <div
                        className="flex shrink-0 items-center gap-1 opacity-0 pointer-events-none transition-opacity group-hover:opacity-100 group-hover:pointer-events-auto focus-within:opacity-100 focus-within:pointer-events-auto"
                        onClick={(e) => e.stopPropagation()}
                      >
                        <button
                          onClick={(e) => openEditModal(wallet, e)}
                          className="rounded-lg p-1.5 text-ink-400 transition-colors hover:bg-primary-50 hover:text-primary-600"
                          title={t('editWallet')}
                        >
                          <PencilIcon className="h-4 w-4" />
                        </button>
                        <button
                          onClick={(e) => confirmDeleteWallet(wallet, e)}
                          className="rounded-lg p-1.5 text-ink-400 transition-colors hover:bg-rose-50 hover:text-rose-600"
                          title={t('deleteWallet')}
                        >
                          <TrashIcon className="h-4 w-4" />
                        </button>
                      </div>
                    </div>

                    {/* Balance */}
                    <div className="mt-3 flex items-baseline justify-between pl-2">
                      <p className="font-[var(--font-dash-mono)] text-xl font-bold text-ink-900">
                        {formatCurrency(wallet.balance, wallet.currency)}
                      </p>
                      {wallet.targetAmount != null && wallet.targetAmount > 0 && (
                        <p className="text-xs text-ink-500">
                          {t('of')} {formatCurrency(wallet.targetAmount, wallet.currency)}
                        </p>
                      )}
                    </div>

                    {/* Progress bar (if target) */}
                    {wallet.targetAmount != null && wallet.targetAmount > 0 && (
                      <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-ink-200 pl-2">
                        <div
                          className="h-full rounded-full transition-all"
                          style={{
                            width: `${Math.min((wallet.balance / wallet.targetAmount) * 100, 100)}%`,
                            backgroundColor: color,
                          }}
                        />
                      </div>
                    )}

                    {/* Footer */}
                    <div className="mt-3 flex justify-end pl-2">
                      <span className="inline-flex items-center gap-1 text-xs font-semibold text-primary-600">
                        {t('viewAll')}
                        <ArrowRightIcon className="h-3.5 w-3.5" />
                      </span>
                    </div>
                  </div>
                );
              })}
            </div>
          </>
        )}
      </div>

      {/* Fix #1: Delete Confirmation Dialog */}
      <ConfirmationDialog
        isOpen={walletToDelete !== null}
        onClose={() => setWalletToDelete(null)}
        onConfirm={handleDelete}
        title={t('deleteWallet')}
        description={walletToDelete ? t('deleteConfirm', { name: walletToDelete.name }) : ''}
        confirmText={t('deleteWallet')}
        cancelText={tCommon('cancel')}
        variant="danger"
      />

      {/* Create/Edit Modal */}
      <BaseModal
        isOpen={showModal}
        onClose={() => setShowModal(false)}
        title={editingWallet ? t('editWallet') : t('createWallet')}
      >
        <div className="space-y-5">
          {/* Name */}
          <div>
            <Label htmlFor="walletName" className="text-sm font-medium text-ink-700">
              {t('name')}
            </Label>
            <Input
              id="walletName"
              value={formData.name}
              onChange={(e) => setFormData((prev) => ({ ...prev, name: e.target.value }))}
              placeholder={t('namePlaceholder')}
              className="mt-1.5"
            />
          </div>

          {/* Icon picker */}
          <div>
            <Label className="text-sm font-medium text-ink-700">{t('icon')}</Label>
            <div className="mt-1.5 flex flex-wrap gap-1.5">
              {WALLET_ICONS.map((item) => (
                <button
                  key={item.id}
                  type="button"
                  aria-label={t(`walletIcons.${item.labelKey}`)}
                  aria-pressed={formData.icon === item.id}
                  onClick={() => setFormData((prev) => ({ ...prev, icon: item.id }))}
                  className={cn(
                    'flex h-10 w-10 items-center justify-center rounded-xl transition-all',
                    formData.icon === item.id
                      ? 'bg-primary-100 ring-2 ring-primary-500'
                      : 'bg-ink-50 hover:bg-ink-100',
                  )}
                >
                  <item.icon className="h-5 w-5 text-ink-700" />
                </button>
              ))}
            </div>
          </div>

          {/* Color picker */}
          <div>
            <Label className="text-sm font-medium text-ink-700">{t('color')}</Label>
            <div className="mt-1.5 flex flex-wrap gap-2">
              {WALLET_COLORS.map((color) => (
                <button
                  key={color}
                  type="button"
                  aria-label={color}
                  aria-pressed={formData.color === color}
                  onClick={() => setFormData((prev) => ({ ...prev, color }))}
                  className={cn(
                    'h-8 w-8 rounded-full transition-all',
                    formData.color === color
                      ? 'ring-2 ring-offset-2 ring-primary-500 scale-110'
                      : 'hover:scale-105',
                  )}
                  style={{ backgroundColor: color }}
                />
              ))}
            </div>
          </div>

          {/* Currency */}
          <div>
            <Label htmlFor="walletCurrency" className="text-sm font-medium text-ink-700">
              {t('currency')}
            </Label>
            <Select
              id="walletCurrency"
              value={formData.currency}
              onChange={(e) => setFormData((prev) => ({ ...prev, currency: e.target.value }))}
              className="mt-1.5"
            >
              {CURRENCIES.map((cur) => (
                <option key={cur} value={cur}>
                  {cur}
                </option>
              ))}
            </Select>
          </div>

          {/* Target amount */}
          <div>
            <Label htmlFor="walletTarget" className="text-sm font-medium text-ink-700">
              {t('targetAmount')}{' '}
              <span className="text-ink-400">({t('optional')})</span>
            </Label>
            <Input
              id="walletTarget"
              type="number"
              min="0"
              step="0.01"
              value={formData.targetAmount}
              onChange={(e) => setFormData((prev) => ({ ...prev, targetAmount: e.target.value }))}
              placeholder={t('amountPlaceholder')}
              className="mt-1.5"
            />
          </div>

          {/* Submit */}
          <div className="flex justify-end gap-3 pt-2">
            <Button variant="outline" onClick={() => setShowModal(false)}>
              {tCommon('cancel')}
            </Button>
            <Button
              onClick={handleSubmit}
              disabled={isSubmitting || !formData.name.trim()}
            >
              {isSubmitting
                ? editingWallet
                  ? t('saving')
                  : t('creating')
                : editingWallet
                  ? t('save')
                  : t('createWallet')}
            </Button>
          </div>
        </div>
      </BaseModal>
    </AppLayout>
  );
}
