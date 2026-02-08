'use client';

import { useState, useEffect } from 'react';
import { BaseModal } from './base-modal';
import { Button } from '@/components/ui/button';
import { apiClient, AccountShareDto } from '@/lib/api-client';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { TrashIcon } from '@heroicons/react/24/outline';

interface ShareAccountModalProps {
  isOpen: boolean;
  onClose: () => void;
  accountId: number;
  accountName: string;
}

export function ShareAccountModal({
  isOpen,
  onClose,
  accountId,
  accountName,
}: ShareAccountModalProps) {
  const t = useTranslations('accounts.sharing');
  const tCommon = useTranslations('common');
  const [email, setEmail] = useState('');
  const [role, setRole] = useState<number>(1); // Default to Viewer
  const [sending, setSending] = useState(false);
  const [shares, setShares] = useState<AccountShareDto[]>([]);
  const [loadingShares, setLoadingShares] = useState(false);
  const [revokeConfirm, setRevokeConfirm] = useState<{ show: boolean; share?: AccountShareDto }>({ show: false });

  useEffect(() => {
    if (isOpen) {
      loadShares();
      setEmail('');
      setRole(1);
    }
  }, [isOpen, accountId]);

  const loadShares = async () => {
    try {
      setLoadingShares(true);
      const data = await apiClient.getAccountShares(accountId);
      setShares(data || []);
    } catch {
      setShares([]);
    } finally {
      setLoadingShares(false);
    }
  };

  const handleSendInvitation = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email.trim()) return;

    try {
      setSending(true);
      await apiClient.createAccountShare(accountId, email.trim(), role);
      toast.success(t('invitationSent'), {
        description: t('invitationSentDetails', { email: email.trim() }),
      });
      setEmail('');
      setRole(1);
      loadShares();
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Failed to send invitation';
      toast.error(message);
    } finally {
      setSending(false);
    }
  };

  const handleRevokeAccess = async (share: AccountShareDto) => {
    try {
      await apiClient.revokeAccountShare(accountId, share.id);
      toast.success(t('accessRevoked'));
      setRevokeConfirm({ show: false });
      loadShares();
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Failed to revoke access';
      toast.error(message);
    }
  };

  const handleRoleChange = async (share: AccountShareDto, newRole: number) => {
    try {
      await apiClient.updateAccountShareRole(accountId, share.id, newRole);
      toast.success(t('roleUpdated'));
      loadShares();
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Failed to update role';
      toast.error(message);
    }
  };

  const getStatusLabel = (status: number) => {
    switch (status) {
      case 1: return t('pendingInvitation');
      case 2: return t('accepted');
      case 3: return t('declined');
      case 4: return t('revoked');
      default: return '';
    }
  };

  const getStatusColor = (status: number) => {
    switch (status) {
      case 1: return 'bg-yellow-100 text-yellow-800';
      case 2: return 'bg-green-100 text-green-800';
      case 3: return 'bg-red-100 text-red-800';
      case 4: return 'bg-gray-100 text-gray-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  /** Returns true for shares that are still actionable (pending or accepted). */
  const isActiveShare = (status: number) => status === 1 || status === 2;

  return (
    <>
      <BaseModal
        isOpen={isOpen}
        onClose={onClose}
        title={`${t('shareAccount')} - ${accountName}`}
        size="md"
      >
        <div className="space-y-6">
          {/* Invite Form */}
          <form onSubmit={handleSendInvitation} className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                {t('shareWith')}
              </label>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder={t('emailPlaceholder')}
                required
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-primary-500 focus:ring-1 focus:ring-primary-500 outline-none"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                {t('selectRole')}
              </label>
              <select
                value={role}
                onChange={(e) => setRole(Number(e.target.value))}
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-primary-500 focus:ring-1 focus:ring-primary-500 outline-none"
              >
                <option value={1}>{t('roleViewer')}</option>
                <option value={2}>{t('roleManager')}</option>
              </select>
            </div>

            <Button
              type="submit"
              disabled={sending || !email.trim()}
              className="w-full bg-primary-600 hover:bg-primary-700 text-white"
            >
              {sending ? '...' : t('sendInvitation')}
            </Button>
          </form>

          {/* Current Shares List */}
          <div>
            <h4 className="text-sm font-semibold text-gray-900 mb-3">
              {t('currentShares')}
            </h4>

            {loadingShares ? (
              <div className="space-y-2">
                {Array.from({ length: 2 }).map((_, i) => (
                  <div key={i} className="animate-pulse flex items-center gap-3 p-3 bg-gray-50 rounded-lg">
                    <div className="h-4 bg-gray-300 rounded w-1/3"></div>
                    <div className="h-4 bg-gray-300 rounded w-1/4"></div>
                  </div>
                ))}
              </div>
            ) : shares.filter((s) => isActiveShare(s.status)).length === 0 ? (
              <p className="text-sm text-gray-500 text-center py-4">
                {t('noShares')}
              </p>
            ) : (
              <div className="space-y-2">
                {shares
                  .filter((share) => isActiveShare(share.status))
                  .map((share) => (
                  <div
                    key={share.id}
                    className="flex items-center justify-between p-3 bg-gray-50 rounded-lg"
                  >
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-medium text-gray-900 truncate">
                        {share.sharedWithUserName || share.sharedWithUserEmail}
                      </p>
                      <div className="flex items-center gap-2 mt-1">
                        <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${getStatusColor(share.status)}`}>
                          {getStatusLabel(share.status)}
                        </span>
                      </div>
                    </div>

                    <div className="flex items-center gap-2 flex-shrink-0 ml-3">
                      <select
                        value={share.role}
                        onChange={(e) => handleRoleChange(share, Number(e.target.value))}
                        className="rounded-md border border-gray-300 px-2 py-1 text-xs focus:border-primary-500 focus:ring-1 focus:ring-primary-500 outline-none"
                      >
                        <option value={1}>{t('roleViewer')}</option>
                        <option value={2}>{t('roleManager')}</option>
                      </select>

                      <button
                        onClick={() => setRevokeConfirm({ show: true, share })}
                        className="p-1 text-red-500 hover:text-red-700 hover:bg-red-50 rounded cursor-pointer"
                        title={t('revokeAccess')}
                      >
                        <TrashIcon className="w-4 h-4" />
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </BaseModal>

      <ConfirmationDialog
        isOpen={revokeConfirm.show}
        title={t('revokeAccess')}
        description={t('revokeConfirm', { name: revokeConfirm.share?.sharedWithUserName || revokeConfirm.share?.sharedWithUserEmail || '' })}
        confirmText={t('revokeAccess')}
        cancelText={tCommon('cancel')}
        variant="danger"
        onConfirm={() => revokeConfirm.share && handleRevokeAccess(revokeConfirm.share)}
        onClose={() => setRevokeConfirm({ show: false })}
      />
    </>
  );
}
