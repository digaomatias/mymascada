'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { useEffect, useMemo, useState } from 'react';
import { AppLayout } from '@/components/app-layout';
import { Button } from '@/components/ui/button';
import { formatCurrency, cn, BackendAccountType } from '@/lib/utils';
import { AccountTypeBadge } from '@/components/ui/account-type-badge';
import { getAccountTypeStyle } from '@/lib/account-styles';
import { apiClient, ReceivedShareDto } from '@/lib/api-client';
import { CheckIcon, XMarkIcon, EnvelopeIcon } from '@heroicons/react/24/outline';
import Link from 'next/link';
import { toast } from 'sonner';
import {
  BuildingOffice2Icon,
  EllipsisVerticalIcon,
  PencilIcon,
  TrashIcon,
  DocumentArrowUpIcon,
  UserGroupIcon,
} from '@heroicons/react/24/outline';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { AddAccountButton } from '@/components/buttons/add-account-button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { ShareAccountModal } from '@/components/modals/share-account-modal';
import { useTranslations } from 'next-intl';

interface Account {
  id: number;
  name: string;
  type: number; // Backend returns numeric enum value
  institution?: string;
  currentBalance: number;
  calculatedBalance: number;
  currency: string;
  isActive: boolean;
  notes?: string;
  createdAt: string;
  updatedAt: string;
  isOwner: boolean;
  isSharedWithMe: boolean;
  shareRole?: number;
  sharedByUserName?: string;
}

export default function AccountsPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const t = useTranslations('accounts');
  const tCommon = useTranslations('common');
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [loading, setLoading] = useState(true);
  const [archiveConfirm, setArchiveConfirm] = useState<{ show: boolean; account?: Account }>({ show: false });
  const [deleteConfirm, setDeleteConfirm] = useState<{ show: boolean; account?: Account }>({ show: false });
  const [shareModal, setShareModal] = useState<{ show: boolean; account?: Account }>({ show: false });
  const [pendingInvitations, setPendingInvitations] = useState<ReceivedShareDto[]>([]);
  const [processingShareIds, setProcessingShareIds] = useState<Set<number>>(new Set());

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  useEffect(() => {
    if (isAuthenticated && typeof window !== 'undefined') {
      loadAccounts();
      loadPendingInvitations();
    }
  }, [isAuthenticated]);

  const loadAccounts = async () => {
    try {
      setLoading(true);
      const accountsData = await apiClient.getAccountsWithBalances() as Account[];
      setAccounts(accountsData || []);
    } catch (error) {
      console.error('Failed to load accounts:', error);
      setAccounts([]);
    } finally {
      setLoading(false);
    }
  };

  // Compute net worth (assets - liabilities)
  const { totalAssets, totalLiabilities, netWorth } = useMemo(() => {
    const liabilityTypes = new Set<number>([BackendAccountType.CreditCard, BackendAccountType.Loan]);
    let assets = 0;
    let liabilities = 0;
    for (const a of accounts) {
      if (liabilityTypes.has(a.type)) {
        liabilities += Math.abs(a.calculatedBalance);
      } else {
        assets += a.calculatedBalance;
      }
    }
    return { totalAssets: assets, totalLiabilities: liabilities, netWorth: assets - liabilities };
  }, [accounts]);

  const activeCount = useMemo(() => accounts.filter((a) => a.isActive).length, [accounts]);

  const loadPendingInvitations = async () => {
    try {
      const shares = await apiClient.getReceivedShares();
      setPendingInvitations((shares || []).filter(s => s.status === 1));
    } catch (error) {
      console.error('Failed to load pending invitations:', error);
    }
  };

  const handleAcceptInvitation = async (shareId: number) => {
    if (processingShareIds.has(shareId)) return;
    setProcessingShareIds(prev => new Set(prev).add(shareId));
    try {
      await apiClient.acceptShareById(shareId);
      toast.success(t('sharing.invitationAccepted'));
      loadAccounts();
      loadPendingInvitations();
    } catch (error) {
      console.error('Failed to accept invitation:', error);
      toast.error(t('sharing.acceptFailed'));
    } finally {
      setProcessingShareIds(prev => {
        const next = new Set(prev);
        next.delete(shareId);
        return next;
      });
    }
  };

  const handleDeclineInvitation = async (shareId: number) => {
    if (processingShareIds.has(shareId)) return;
    setProcessingShareIds(prev => new Set(prev).add(shareId));
    try {
      await apiClient.declineShareById(shareId);
      toast.success(t('sharing.invitationDeclined'));
      loadPendingInvitations();
    } catch (error) {
      console.error('Failed to decline invitation:', error);
      toast.error(t('sharing.declineFailed'));
    } finally {
      setProcessingShareIds(prev => {
        const next = new Set(prev);
        next.delete(shareId);
        return next;
      });
    }
  };

  const handleArchiveAccount = async (account: Account) => {
    try {
      await apiClient.archiveAccount(account.id);
      toast.success(t('archivedSuccess', { name: account.name }));
      loadAccounts();
      setArchiveConfirm({ show: false });
    } catch (error: unknown) {
      console.error('Failed to archive account:', error);
      if (error instanceof Error && error.message?.includes('transactions')) {
        toast.error(t('archiveHasTransactions'));
      } else {
        toast.error(t('archiveFailed'));
      }
    }
  };

  const handleDeleteAccount = async (account: Account) => {
    try {
      await apiClient.deleteAccount(account.id);
      loadAccounts();
      toast.success(t('deletedSuccess', { name: account.name }));
      setDeleteConfirm({ show: false });
    } catch (error: unknown) {
      console.error('Failed to delete account:', error);
      toast.error(t('deleteFailed'));
    }
  };

  const getShareRoleName = (role?: number) => {
    return role === 2 ? t('sharing.roleManager') : t('sharing.roleViewer');
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <BuildingOffice2Icon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">{t('loading')}</div>
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
      <header className="flex flex-wrap items-end justify-between gap-4 mb-5">
        <div>
          <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900 sm:text-[2.1rem]">
            {t('title')}
          </h1>
          <p className="mt-1.5 text-[15px] text-slate-500">
            {t('subtitle')}
          </p>
        </div>
        <AddAccountButton
          onSuccess={loadAccounts}
          className="flex items-center gap-2"
        />
      </header>

      <div className="space-y-5">
        {/* Hero net worth section */}
        {!loading && accounts.length > 0 && (
          <section className="rounded-[26px] border border-violet-100/60 bg-white/90 p-6 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div>
                <p className="text-sm font-semibold text-slate-500">{t('netWorth')}</p>
                <div className="mt-2 flex items-baseline gap-3">
                  <p className="font-[var(--font-dash-mono)] text-5xl font-semibold tracking-[-0.02em] text-slate-900 sm:text-[3.2rem]">
                    {formatCurrency(netWorth)}
                  </p>
                </div>
              </div>
              {/* Quick stats */}
              <div className="flex items-center gap-4">
                <div className="text-right">
                  <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                    {t('totalAccounts')}
                  </p>
                  <p className="mt-1 font-[var(--font-dash-mono)] text-xl font-semibold text-slate-900">
                    {accounts.length}
                  </p>
                </div>
                <div className="h-8 w-px bg-slate-200" />
                <div className="text-right">
                  <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                    {t('activeAccounts')}
                  </p>
                  <p className="mt-1 font-[var(--font-dash-mono)] text-xl font-semibold text-slate-900">
                    {activeCount}
                  </p>
                </div>
              </div>
            </div>

            {/* Asset / Liability breakdown bar */}
            <div className="mt-5">
              <div className="flex items-center justify-between text-xs font-medium text-slate-400">
                <span className="text-emerald-600">{formatCurrency(totalAssets)}</span>
                <span className="text-rose-500">{formatCurrency(totalLiabilities)}</span>
              </div>
              <div className="mt-1.5 flex h-3 overflow-hidden rounded-full bg-slate-100">
                {totalAssets + totalLiabilities > 0 && (
                  <>
                    <div
                      className="h-full rounded-l-full bg-gradient-to-r from-emerald-400 to-emerald-500 transition-all duration-700"
                      style={{ width: `${(totalAssets / (totalAssets + totalLiabilities)) * 100}%` }}
                    />
                    <div
                      className="h-full rounded-r-full bg-gradient-to-r from-rose-400 to-rose-500 transition-all duration-700"
                      style={{ width: `${(totalLiabilities / (totalAssets + totalLiabilities)) * 100}%` }}
                    />
                  </>
                )}
              </div>
            </div>
          </section>
        )}

        {/* Pending Invitations */}
        {pendingInvitations.length > 0 && (
          <section className="rounded-2xl border border-amber-200/70 bg-amber-50/50 p-4">
            <div className="flex items-center gap-2 mb-3">
              <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-amber-100 text-amber-700">
                <EnvelopeIcon className="h-[18px] w-[18px]" />
              </div>
              <h2 className="text-sm font-semibold text-slate-800">
                {t('sharing.pendingInvitations')}
              </h2>
              <span className="inline-flex items-center justify-center h-5 min-w-5 rounded-full bg-amber-200 px-1.5 text-[11px] font-bold text-amber-800">
                {pendingInvitations.length}
              </span>
            </div>
            <div className="space-y-2">
              {pendingInvitations.map((invitation) => (
                <div
                  key={invitation.id}
                  className="flex flex-col sm:flex-row sm:items-center gap-3 rounded-xl border border-amber-200/50 bg-white/80 p-3.5"
                >
                  <div className="flex items-center gap-3 min-w-0 flex-1">
                    <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-amber-400 to-amber-500">
                      <BuildingOffice2Icon className="h-5 w-5 text-white" />
                    </div>
                    <div className="min-w-0">
                      <h4 className="text-sm font-semibold text-slate-900 truncate">{invitation.accountName}</h4>
                      <p className="text-xs text-slate-500 mt-0.5">
                        {t('sharing.sharedByName', { name: invitation.sharedByName })}
                        {' \u00B7 '}
                        {getShareRoleName(invitation.role)}
                      </p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2 pl-13 sm:pl-0">
                    <Button
                      size="sm"
                      onClick={() => handleAcceptInvitation(invitation.id)}
                      disabled={processingShareIds.has(invitation.id)}
                    >
                      <CheckIcon className="w-4 h-4 mr-1" />
                      {t('sharing.accept')}
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => handleDeclineInvitation(invitation.id)}
                      disabled={processingShareIds.has(invitation.id)}
                    >
                      <XMarkIcon className="w-4 h-4 mr-1" />
                      {t('sharing.decline')}
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          </section>
        )}

        {/* Accounts list */}
        {loading ? (
          <div className="space-y-4">
            {[1, 2, 3].map((i) => (
              <div
                key={i}
                className="h-28 animate-pulse rounded-[26px] border border-violet-100/80 bg-white/80"
              />
            ))}
          </div>
        ) : accounts.length === 0 ? (
          /* Empty state */
          <section className="rounded-[28px] border border-violet-100/80 bg-white/92 p-10 text-center shadow-[0_20px_50px_-30px_rgba(76,29,149,0.45)]">
            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-violet-100 text-violet-600">
              <BuildingOffice2Icon className="h-7 w-7" />
            </div>
            <h3 className="mt-4 text-xl font-semibold text-slate-900">{t('noAccounts')}</h3>
            <p className="mt-2 text-sm text-slate-500">
              {t('noAccountsDescription')}
            </p>
            <div className="mt-5 inline-flex">
              <AddAccountButton
                onSuccess={loadAccounts}
                className="flex items-center gap-2"
              >
                {t('createFirst')}
              </AddAccountButton>
            </div>
          </section>
        ) : (
          <div className="space-y-3">
            {accounts.map((account) => (
              <AccountCard
                key={account.id}
                account={account}
                onClick={() => router.push(`/accounts/${account.id}`)}
                onShare={(a) => setShareModal({ show: true, account: a })}
                onArchive={(a) => setArchiveConfirm({ show: true, account: a })}
                onDelete={(a) => setDeleteConfirm({ show: true, account: a })}
                getShareRoleName={getShareRoleName}
                t={t}
              />
            ))}
          </div>
        )}
      </div>

      {/* Archive Confirmation Dialog */}
      <ConfirmationDialog
        isOpen={archiveConfirm.show}
        title={t('archiveAccount')}
        description={t('archiveConfirmDetails', { name: archiveConfirm.account?.name || '' })}
        confirmText={t('archiveAccount')}
        cancelText={tCommon('cancel')}
        variant="default"
        onConfirm={() => archiveConfirm.account && handleArchiveAccount(archiveConfirm.account)}
        onClose={() => setArchiveConfirm({ show: false })}
      />

      {/* Delete Confirmation Dialog */}
      <ConfirmationDialog
        isOpen={deleteConfirm.show}
        title={t('permanentDelete')}
        description={t('permanentDeleteConfirm', { name: deleteConfirm.account?.name || '' })}
        confirmText={t('deletePermanently')}
        cancelText={tCommon('cancel')}
        variant="danger"
        onConfirm={() => deleteConfirm.account && handleDeleteAccount(deleteConfirm.account)}
        onClose={() => setDeleteConfirm({ show: false })}
      />

      {/* Share Account Modal */}
      {shareModal.account && (
        <ShareAccountModal
          isOpen={shareModal.show}
          onClose={() => setShareModal({ show: false })}
          accountId={shareModal.account.id}
          accountName={shareModal.account.name}
        />
      )}
    </AppLayout>
  );
}

// --- Account Card ---

function AccountCard({
  account,
  onClick,
  onShare,
  onArchive,
  onDelete,
  getShareRoleName,
  t,
}: {
  account: Account;
  onClick: () => void;
  onShare: (account: Account) => void;
  onArchive: (account: Account) => void;
  onDelete: (account: Account) => void;
  getShareRoleName: (role?: number) => string;
  t: ReturnType<typeof useTranslations<'accounts'>>;
}) {
  const style = getAccountTypeStyle(account.type);
  const Icon = style.icon;

  return (
    <article
      onClick={onClick}
      className="cursor-pointer rounded-[26px] border border-violet-100/80 bg-white/90 p-5 shadow-[0_20px_44px_-32px_rgba(76,29,149,0.48)] transition-shadow hover:shadow-[0_24px_52px_-28px_rgba(76,29,149,0.55)]"
    >
      <div className="flex flex-col sm:flex-row sm:items-center gap-3 sm:gap-4">
        {/* Left: icon + name + badges */}
        <div className="flex items-center gap-3 min-w-0 flex-1">
          <div
            className={cn(
              'flex h-10 w-10 sm:h-11 sm:w-11 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br',
              style.gradient,
            )}
          >
            <Icon className="h-5 w-5 sm:h-6 sm:w-6 text-white" />
          </div>

          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2">
              <h3 className="line-clamp-1 text-lg font-semibold tracking-[-0.02em] text-slate-900">
                {account.name}
              </h3>
            </div>
            <div className="mt-0.5 flex flex-wrap items-center gap-1.5">
              <AccountTypeBadge type={account.type} />
              {account.institution && (
                <span className="text-xs text-slate-500 truncate">{account.institution}</span>
              )}
              {account.isSharedWithMe && (
                <span className="inline-flex items-center gap-1 rounded-full border border-blue-200 bg-blue-50 px-2 py-0.5 text-[11px] font-medium text-blue-700">
                  <UserGroupIcon className="h-3 w-3" />
                  {t('sharing.sharedByName', { name: account.sharedByUserName || '' })}
                  {' \u00B7 '}
                  {getShareRoleName(account.shareRole)}
                </span>
              )}
            </div>
          </div>
        </div>

        {/* Right: balance + menu */}
        <div className="flex items-center justify-between sm:justify-end gap-3 pl-13 sm:pl-0">
          <div className="text-left sm:text-right">
            <p className="font-[var(--font-dash-mono)] text-xl font-bold tracking-tight text-slate-900">
              {formatCurrency(account.calculatedBalance)}
            </p>
            <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-400">
              {account.currency}
            </p>
          </div>

          {/* Action Menu */}
          <div className="shrink-0" onClick={(e) => e.stopPropagation()}>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-8 w-8 rounded-lg p-0 text-slate-400 transition-colors hover:bg-violet-50 hover:text-violet-600"
                >
                  <EllipsisVerticalIcon className="h-4 w-4" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-48 bg-white border border-gray-200 shadow-lg z-50">
                {(!account.isSharedWithMe || account.shareRole === 2) && (
                  <DropdownMenuItem asChild>
                    <Link
                      href={`/transactions/new?accountId=${account.id}`}
                      className="flex items-center gap-2 cursor-pointer px-3 py-2 text-sm text-gray-700 hover:bg-gray-50 rounded-sm"
                    >
                      <DocumentArrowUpIcon className="w-4 h-4" />
                      {t('addTransaction')}
                    </Link>
                  </DropdownMenuItem>
                )}
                {account.isOwner && (
                  <>
                    <DropdownMenuItem
                      onClick={() => onShare(account)}
                      className="flex items-center gap-2 px-3 py-2 text-sm cursor-pointer"
                    >
                      <UserGroupIcon className="w-4 h-4" />
                      {t('sharing.shareAccount')}
                    </DropdownMenuItem>
                    <DropdownMenuItem asChild>
                      <Link
                        href={`/accounts/${account.id}/edit`}
                        className="flex items-center gap-2 cursor-pointer px-3 py-2 text-sm text-gray-700 hover:bg-gray-50 rounded-sm"
                      >
                        <PencilIcon className="w-4 h-4" />
                        {t('editAccount')}
                      </Link>
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      variant="destructive"
                      onClick={() => onArchive(account)}
                      className="flex items-center gap-2 px-3 py-2 text-sm cursor-pointer"
                    >
                      <TrashIcon className="w-4 h-4" />
                      {t('archiveAccount')}
                    </DropdownMenuItem>
                    <DropdownMenuSeparator className="my-1 bg-gray-200" />
                    <DropdownMenuItem
                      variant="destructive"
                      onClick={() => onDelete(account)}
                      className="flex items-center gap-2 px-3 py-2 text-sm cursor-pointer"
                    >
                      <TrashIcon className="w-4 h-4" />
                      {t('deletePermanently')}
                    </DropdownMenuItem>
                  </>
                )}
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
      </div>
    </article>
  );
}

// Force dynamic rendering for this page
export const dynamic = 'force-dynamic';
