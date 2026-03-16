'use client';

import { useEffect, useState, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { AppLayout } from '@/components/app-layout';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { BaseModal } from '@/components/modals/base-modal';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  apiClient,
  WalletDetail,
  WalletAllocation,
  UpdateWalletRequest,
  CreateAllocationRequest,
} from '@/lib/api-client';
import { cn, formatCurrency, formatDate } from '@/lib/utils';
import { toast } from 'sonner';
import {
  PencilIcon,
  TrashIcon,
  PlusIcon,
  CalendarDaysIcon,
  BuildingOffice2Icon,
} from '@heroicons/react/24/outline';
import { BackButton } from '@/components/ui/back-button';
import { useAuthGuard } from '@/hooks/use-auth-guard';

const WALLET_EMOJIS = [
  '\u{1F4B0}', '\u{1F4B5}', '\u{1F4B3}', '\u{1F3E6}', '\u{1F3AF}',
  '\u{1F48E}', '\u{1F437}', '\u{1F3E0}', '\u{1F393}', '\u{2708}\u{FE0F}',
  '\u{1F3E5}', '\u{1F6CD}\u{FE0F}', '\u{1F381}', '\u{1F37D}\u{FE0F}', '\u{2615}',
  '\u{1F697}', '\u{1F4F1}', '\u{1F4BB}', '\u{1F3AE}', '\u{1F3CB}\u{FE0F}\u{200D}\u{2642}\u{FE0F}',
  '\u{1F4DA}', '\u{1F476}', '\u{1F43E}', '\u{1F3B5}', '\u{1F31F}',
];

const WALLET_COLORS = [
  '#7c3aed', '#2563eb', '#059669', '#d97706',
  '#dc2626', '#db2777', '#0891b2', '#4f46e5',
  '#65a30d', '#ea580c', '#6d28d9', '#0d9488',
];

const CURRENCIES = ['NZD', 'USD', 'EUR', 'BRL', 'GBP', 'AUD'];

const DEFAULT_ICON = '\u{1F4B0}';
const DEFAULT_COLOR = '#7c3aed';

interface WalletFormData {
  name: string;
  icon: string;
  color: string;
  currency: string;
  targetAmount: string;
}

export default function WalletDetailPage() {
  const { shouldRender, isAuthResolved } = useAuthGuard();
  const params = useParams();
  const router = useRouter();
  const t = useTranslations('wallets');
  const tCommon = useTranslations('common');

  const walletId = Number(params.id);
  const hasValidWalletId = Number.isInteger(walletId) && walletId > 0;
  const [wallet, setWallet] = useState<WalletDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [allocationToRemove, setAllocationToRemove] = useState<WalletAllocation | null>(null);
  const [showEditModal, setShowEditModal] = useState(false);
  const [showAllocateForm, setShowAllocateForm] = useState(false);
  const [editFormData, setEditFormData] = useState<WalletFormData>({
    name: '',
    icon: '\u{1F4B0}',
    color: '#7c3aed',
    currency: 'NZD',
    targetAmount: '',
  });
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Allocation form state
  const [allocTransactionId, setAllocTransactionId] = useState('');
  const [allocAmount, setAllocAmount] = useState('');
  const [allocNote, setAllocNote] = useState('');
  const [isAllocating, setIsAllocating] = useState(false);

  const loadWallet = useCallback(async () => {
    try {
      setIsLoading(true);
      const data = await apiClient.getWallet(walletId);
      setWallet(data);
    } catch {
      toast.error(t('loadError'));
      router.push('/wallets');
    } finally {
      setIsLoading(false);
    }
  }, [walletId, router, t]);

  // Fix #5: Handle invalid route IDs
  useEffect(() => {
    if (!isAuthResolved) return;
    if (!hasValidWalletId) {
      router.push('/wallets');
      return;
    }
    loadWallet();
  }, [isAuthResolved, hasValidWalletId, loadWallet, router]);

  const handleDelete = async () => {
    try {
      await apiClient.deleteWallet(walletId);
      toast.success(t('walletDeleted'));
      router.push('/wallets');
    } catch {
      toast.error(t('deleteError'));
    }
  };

  const openEditModal = () => {
    if (!wallet) return;
    setEditFormData({
      name: wallet.name,
      icon: wallet.icon || DEFAULT_ICON,
      color: wallet.color || DEFAULT_COLOR,
      currency: wallet.currency,
      targetAmount: wallet.targetAmount?.toString() ?? '',
    });
    setShowEditModal(true);
  };

  const handleEditSubmit = async () => {
    if (!editFormData.name.trim()) return;

    setIsSubmitting(true);
    try {
      // Fix #10: When target amount is cleared, set clearTargetAmount: true
      const hadTarget = wallet?.targetAmount != null && wallet.targetAmount > 0;
      const newTarget = editFormData.targetAmount ? parseFloat(editFormData.targetAmount) : null;
      const update: UpdateWalletRequest = {
        name: editFormData.name.trim(),
        icon: editFormData.icon,
        color: editFormData.color,
        currency: editFormData.currency,
        targetAmount: newTarget,
        clearTargetAmount: hadTarget && !newTarget ? true : undefined,
      };
      const updated = await apiClient.updateWallet(walletId, update);
      setWallet(updated);
      setShowEditModal(false);
      toast.success(t('walletUpdated'));
    } catch {
      toast.error(t('updateError'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleAllocate = async () => {
    const txId = parseInt(allocTransactionId, 10);
    const amount = parseFloat(allocAmount);
    if (isNaN(txId) || txId <= 0 || isNaN(amount) || amount === 0) return;

    setIsAllocating(true);
    try {
      const allocation: CreateAllocationRequest = {
        transactionId: txId,
        amount,
        note: allocNote.trim() || undefined,
      };
      await apiClient.createWalletAllocation(walletId, allocation);
      toast.success(t('allocationCreated'));
      setAllocTransactionId('');
      setAllocAmount('');
      setAllocNote('');
      setShowAllocateForm(false);
      loadWallet();
    } catch {
      toast.error(t('allocationError'));
    } finally {
      setIsAllocating(false);
    }
  };

  const handleRemoveAllocation = async () => {
    if (!allocationToRemove) return;

    try {
      await apiClient.deleteWalletAllocation(walletId, allocationToRemove.id);
      toast.success(t('allocationRemoved'));
      loadWallet();
    } catch {
      toast.error(t('removeError'));
    } finally {
      setAllocationToRemove(null);
    }
  };

  if (!shouldRender) return null;

  if (isLoading) {
    return (
      <AppLayout>
        <div className="space-y-6">
          <Skeleton className="h-8 w-48 rounded-[26px] border border-violet-100/80 bg-white/80" />
          <Skeleton className="h-12 w-80 rounded-[26px] border border-violet-100/80 bg-white/80" />
          <Skeleton className="h-40 rounded-[26px] border border-violet-100/80 bg-white/80" />
          <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
            <Skeleton className="h-24 rounded-2xl border border-violet-100/80 bg-white/80" />
            <Skeleton className="h-24 rounded-2xl border border-violet-100/80 bg-white/80" />
            <Skeleton className="h-24 rounded-2xl border border-violet-100/80 bg-white/80" />
          </div>
          <Skeleton className="h-48 rounded-[24px] border border-violet-100/80 bg-white/80" />
        </div>
      </AppLayout>
    );
  }

  if (!wallet) {
    return null;
  }

  // Fix #4: Null safety for icon and color
  const walletIcon = wallet.icon || DEFAULT_ICON;
  const walletColor = wallet.color || DEFAULT_COLOR;

  const progressPercent =
    wallet.targetAmount && wallet.targetAmount > 0
      ? Math.min((wallet.balance / wallet.targetAmount) * 100, 100)
      : null;

  return (
    <AppLayout>
      <div className="space-y-6">
        {/* Header */}
        <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="space-y-3">
            <BackButton variant="link" href="/wallets" label={t('backToWallets')} />

            {/* Title row */}
            <div className="flex items-center gap-3">
              <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl text-2xl" style={{ backgroundColor: `${walletColor}20` }}>
                {walletIcon}
              </span>
              <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900">
                {wallet.name}
              </h1>
            </div>

            {/* Badges row */}
            <div className="flex flex-wrap items-center gap-2">
              {wallet.isArchived ? (
                <span className="inline-flex items-center rounded-full border border-slate-200 bg-slate-50 px-2.5 py-0.5 text-xs font-medium text-slate-500">
                  {t('archived')}
                </span>
              ) : (
                <span className="inline-flex items-center rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-0.5 text-xs font-medium text-emerald-700">
                  {t('active')}
                </span>
              )}
              <span className="inline-flex items-center rounded-full border border-slate-200 bg-slate-50 px-2.5 py-0.5 text-xs font-medium text-slate-600">
                {wallet.currency}
              </span>
            </div>
          </div>

          {/* Actions */}
          <div className="flex flex-wrap items-center gap-2">
            <Button variant="outline" onClick={openEditModal}>
              <PencilIcon className="h-4 w-4 mr-2" />
              {tCommon('edit')}
            </Button>
            <Button
              variant="outline"
              className="text-destructive hover:text-destructive"
              onClick={() => setShowDeleteDialog(true)}
            >
              <TrashIcon className="h-4 w-4 mr-2" />
              {tCommon('delete')}
            </Button>
          </div>
        </div>

        {/* Balance Hero */}
        <div className="rounded-[26px] border border-violet-100/60 bg-white/90 p-6 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
          <div className="space-y-4">
            <div className="flex items-end justify-between">
              <div>
                <span className="text-sm text-slate-500">{t('balance')}</span>
              </div>
              <span className="font-[var(--font-dash-sans)] text-4xl font-bold tracking-tight text-slate-900">
                {formatCurrency(wallet.balance, wallet.currency)}
              </span>
            </div>
            {progressPercent !== null && (
              <>
                <div className="h-3 overflow-hidden rounded-full bg-slate-100">
                  <div
                    className="h-full rounded-full transition-all duration-500"
                    style={{
                      width: `${progressPercent}%`,
                      backgroundColor: walletColor,
                    }}
                  />
                </div>
                <div className="flex justify-between text-sm text-slate-500">
                  <span>
                    {progressPercent.toFixed(1)}% {t('progress').toLowerCase()}
                  </span>
                  <span>
                    {formatCurrency(wallet.balance, wallet.currency)} {t('of')}{' '}
                    {formatCurrency(wallet.targetAmount!, wallet.currency)}
                  </span>
                </div>
              </>
            )}
          </div>
        </div>

        {/* Stats Grid */}
        <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
          <article className="rounded-2xl border border-slate-100 bg-white/90 p-4 shadow-sm backdrop-blur-xs">
            <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-400">
              {t('balance')}
            </p>
            <p className="mt-1 text-xl font-bold text-slate-900">
              {formatCurrency(wallet.balance, wallet.currency)}
            </p>
          </article>
          {wallet.targetAmount != null && wallet.targetAmount > 0 && (
            <article className="rounded-2xl border border-slate-100 bg-white/90 p-4 shadow-sm backdrop-blur-xs">
              <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-400">
                {t('targetAmount')}
              </p>
              <p className="mt-1 text-xl font-bold text-slate-900">
                {formatCurrency(wallet.targetAmount, wallet.currency)}
              </p>
            </article>
          )}
          <article className="rounded-2xl border border-slate-100 bg-white/90 p-4 shadow-sm backdrop-blur-xs">
            <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-400">
              {t('allocations')}
            </p>
            <p className="mt-1 text-xl font-bold text-slate-900">
              {wallet.allocations.length}
            </p>
          </article>
        </div>

        {/* Allocations Section */}
        <div className="rounded-[24px] border border-slate-100 bg-white/90 p-6 shadow-sm backdrop-blur-xs">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-slate-900">{t('allocations')}</h2>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setShowAllocateForm(!showAllocateForm)}
            >
              <PlusIcon className="h-4 w-4 mr-1.5" />
              {t('allocateTransaction')}
            </Button>
          </div>

          {/* Allocate transaction form */}
          {showAllocateForm && (
            <div className="mb-5 rounded-xl border border-violet-100 bg-violet-50/30 p-4">
              <div className="grid gap-3 sm:grid-cols-3">
                <div>
                  <Label htmlFor="allocTxId" className="text-xs font-medium text-slate-600">
                    {t('transactionId')}
                  </Label>
                  <Input
                    id="allocTxId"
                    type="number"
                    min="1"
                    value={allocTransactionId}
                    onChange={(e) => setAllocTransactionId(e.target.value)}
                    placeholder={t('transactionIdPlaceholder')}
                    className="mt-1"
                  />
                </div>
                <div>
                  <Label htmlFor="allocAmount" className="text-xs font-medium text-slate-600">
                    {t('amount')}
                  </Label>
                  <Input
                    id="allocAmount"
                    type="number"
                    step="0.01"
                    value={allocAmount}
                    onChange={(e) => setAllocAmount(e.target.value)}
                    placeholder={t('amountPlaceholder')}
                    className="mt-1"
                  />
                </div>
                <div>
                  <Label htmlFor="allocNote" className="text-xs font-medium text-slate-600">
                    {t('note')}
                  </Label>
                  <Input
                    id="allocNote"
                    value={allocNote}
                    onChange={(e) => setAllocNote(e.target.value)}
                    placeholder={t('notePlaceholder')}
                    className="mt-1"
                  />
                </div>
              </div>
              <div className="mt-3 flex justify-end gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setShowAllocateForm(false)}
                >
                  {tCommon('cancel')}
                </Button>
                <Button
                  size="sm"
                  onClick={handleAllocate}
                  disabled={
                    isAllocating ||
                    !allocTransactionId ||
                    !allocAmount ||
                    isNaN(parseInt(allocTransactionId, 10)) ||
                    isNaN(parseFloat(allocAmount)) ||
                    parseFloat(allocAmount) === 0
                  }
                >
                  {isAllocating ? t('allocating') : t('allocate')}
                </Button>
              </div>
            </div>
          )}

          {wallet.allocations.length === 0 ? (
            <div className="py-8 text-center">
              <p className="text-sm font-medium text-slate-500">{t('noAllocations')}</p>
              <p className="mt-1 text-xs text-slate-400">{t('noAllocationsDescription')}</p>
            </div>
          ) : (
            <div className="space-y-2">
              {wallet.allocations.map((allocation) => (
                <div
                  key={allocation.id}
                  className="flex items-center gap-3 rounded-xl border border-slate-100 bg-slate-50/50 px-4 py-3 transition-colors hover:bg-slate-50"
                >
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium text-slate-900">
                      {allocation.transactionDescription}
                    </p>
                    <div className="mt-0.5 flex flex-wrap items-center gap-3 text-xs text-slate-500">
                      <span className="inline-flex items-center gap-1">
                        <CalendarDaysIcon className="h-3.5 w-3.5" />
                        {formatDate(allocation.transactionDate)}
                      </span>
                      <span className="inline-flex items-center gap-1">
                        <BuildingOffice2Icon className="h-3.5 w-3.5" />
                        {allocation.accountName}
                      </span>
                      {allocation.note && (
                        <span className="italic text-slate-400">{allocation.note}</span>
                      )}
                    </div>
                  </div>
                  <p className={cn(
                    'shrink-0 font-[var(--font-dash-mono)] text-sm font-semibold',
                    allocation.amount >= 0 ? 'text-emerald-600' : 'text-rose-600',
                  )}>
                    {formatCurrency(allocation.amount, wallet.currency)}
                  </p>
                  <button
                    onClick={() => setAllocationToRemove(allocation)}
                    className="shrink-0 rounded-lg px-2.5 py-1 text-xs font-medium text-rose-600 transition-colors hover:bg-rose-50"
                  >
                    {t('removeAllocation')}
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Delete Confirmation Dialog */}
        <ConfirmationDialog
          isOpen={showDeleteDialog}
          onClose={() => setShowDeleteDialog(false)}
          onConfirm={handleDelete}
          title={t('deleteWallet')}
          description={t('deleteConfirm', { name: wallet.name })}
          confirmText={t('deleteWallet')}
          cancelText={tCommon('cancel')}
          variant="danger"
        />

        {/* Remove Allocation Confirmation Dialog */}
        <ConfirmationDialog
          isOpen={allocationToRemove !== null}
          onClose={() => setAllocationToRemove(null)}
          onConfirm={handleRemoveAllocation}
          title={t('removeAllocation')}
          description={t('removeAllocationConfirm')}
          confirmText={t('removeAllocation')}
          cancelText={tCommon('cancel')}
          variant="danger"
        />

        {/* Edit Modal */}
        <BaseModal
          isOpen={showEditModal}
          onClose={() => setShowEditModal(false)}
          title={t('editWallet')}
        >
          <div className="space-y-5">
            {/* Name */}
            <div>
              <Label htmlFor="editWalletName" className="text-sm font-medium text-slate-700">
                {t('name')}
              </Label>
              <Input
                id="editWalletName"
                value={editFormData.name}
                onChange={(e) => setEditFormData((prev) => ({ ...prev, name: e.target.value }))}
                placeholder={t('namePlaceholder')}
                className="mt-1.5"
              />
            </div>

            {/* Icon picker */}
            <div>
              <Label className="text-sm font-medium text-slate-700">{t('icon')}</Label>
              <div className="mt-1.5 flex flex-wrap gap-1.5">
                {WALLET_EMOJIS.map((emoji) => (
                  <button
                    key={emoji}
                    type="button"
                    aria-label={emoji}
                    aria-pressed={editFormData.icon === emoji}
                    onClick={() => setEditFormData((prev) => ({ ...prev, icon: emoji }))}
                    className={cn(
                      'flex h-10 w-10 items-center justify-center rounded-xl text-xl transition-all',
                      editFormData.icon === emoji
                        ? 'bg-violet-100 ring-2 ring-violet-500'
                        : 'bg-slate-50 hover:bg-slate-100',
                    )}
                  >
                    {emoji}
                  </button>
                ))}
              </div>
            </div>

            {/* Color picker */}
            <div>
              <Label className="text-sm font-medium text-slate-700">{t('color')}</Label>
              <div className="mt-1.5 flex flex-wrap gap-2">
                {WALLET_COLORS.map((color) => (
                  <button
                    key={color}
                    type="button"
                    aria-label={color}
                    aria-pressed={editFormData.color === color}
                    onClick={() => setEditFormData((prev) => ({ ...prev, color }))}
                    className={cn(
                      'h-8 w-8 rounded-full transition-all',
                      editFormData.color === color
                        ? 'ring-2 ring-offset-2 ring-violet-500 scale-110'
                        : 'hover:scale-105',
                    )}
                    style={{ backgroundColor: color }}
                  />
                ))}
              </div>
            </div>

            {/* Currency */}
            <div>
              <Label htmlFor="editWalletCurrency" className="text-sm font-medium text-slate-700">
                {t('currency')}
              </Label>
              <Select
                id="editWalletCurrency"
                value={editFormData.currency}
                onChange={(e) => setEditFormData((prev) => ({ ...prev, currency: e.target.value }))}
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
              <Label htmlFor="editWalletTarget" className="text-sm font-medium text-slate-700">
                {t('targetAmount')}{' '}
                <span className="text-slate-400">({t('optional')})</span>
              </Label>
              <Input
                id="editWalletTarget"
                type="number"
                min="0"
                step="0.01"
                value={editFormData.targetAmount}
                onChange={(e) => setEditFormData((prev) => ({ ...prev, targetAmount: e.target.value }))}
                placeholder={t('amountPlaceholder')}
                className="mt-1.5"
              />
            </div>

            {/* Submit */}
            <div className="flex justify-end gap-3 pt-2">
              <Button variant="outline" onClick={() => setShowEditModal(false)}>
                {tCommon('cancel')}
              </Button>
              <Button
                onClick={handleEditSubmit}
                disabled={isSubmitting || !editFormData.name.trim()}
              >
                {isSubmitting ? t('saving') : t('save')}
              </Button>
            </div>
          </div>
        </BaseModal>
      </div>
    </AppLayout>
  );
}
