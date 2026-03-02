'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import Link from 'next/link';
import { AppLayout } from '@/components/app-layout';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { apiClient, UpdateGoalRequest } from '@/lib/api-client';
import { toast } from 'sonner';
import { ArrowLeftIcon } from '@heroicons/react/24/outline';

interface Account {
  id: number;
  name: string;
}

const GOAL_TYPES = ['EmergencyFund', 'Savings', 'DebtPayoff', 'Investment', 'Custom'] as const;
const STATUSES = ['Active', 'Paused', 'Abandoned'] as const;

export default function EditGoalPage() {
  const params = useParams();
  const router = useRouter();
  const t = useTranslations('goals');
  const tCommon = useTranslations('common');

  const goalId = Number(params.id);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [accounts, setAccounts] = useState<Account[]>([]);

  // Form state
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [targetAmount, setTargetAmount] = useState('');
  const [currentAmount, setCurrentAmount] = useState('');
  const [deadline, setDeadline] = useState('');
  const [goalType, setGoalType] = useState('Savings');
  const [status, setStatus] = useState('Active');
  const [linkedAccountId, setLinkedAccountId] = useState('');

  useEffect(() => {
    const loadData = async () => {
      try {
        setIsLoading(true);
        const [goal, accountsData] = await Promise.all([
          apiClient.getGoal(goalId),
          apiClient.getAccounts() as Promise<Account[]>,
        ]);

        setName(goal.name);
        setDescription(goal.description || '');
        setTargetAmount(goal.targetAmount.toString());
        setCurrentAmount(goal.currentAmount.toString());
        setDeadline(goal.deadline ? goal.deadline.split('T')[0] : '');
        setGoalType(goal.goalType);
        setStatus(goal.status);
        setLinkedAccountId(goal.linkedAccountId?.toString() || '');
        setAccounts(accountsData);
      } catch {
        toast.error(t('loadError'));
        router.push('/goals');
      } finally {
        setIsLoading(false);
      }
    };

    if (goalId) {
      loadData();
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [goalId]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    const target = parseFloat(targetAmount);
    const current = parseFloat(currentAmount);
    if (!name.trim() || isNaN(target) || target <= 0) {
      return;
    }

    try {
      setIsSubmitting(true);
      const request: UpdateGoalRequest = {
        name: name.trim(),
        description: description.trim() || undefined,
        targetAmount: target,
        currentAmount: isNaN(current) ? undefined : current,
        status,
        deadline: deadline || undefined,
        linkedAccountId: linkedAccountId ? parseInt(linkedAccountId) : undefined,
      };

      await apiClient.updateGoal(goalId, request);
      toast.success(t('goalUpdated'));
      router.push(`/goals/${goalId}`);
    } catch {
      toast.error(t('updateError'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const canSubmit = name.trim() !== '' && parseFloat(targetAmount) > 0;

  if (isLoading) {
    return (
      <AppLayout>
        <div className="space-y-4">
          <Skeleton className="h-8 w-64 rounded-2xl" />
          <Skeleton className="h-96 rounded-[26px]" />
        </div>
      </AppLayout>
    );
  }

  return (
    <AppLayout>
        {/* Navigation Bar */}
        <div className="flex items-center justify-between mb-6">
          <Link href={`/goals/${goalId}`}>
            <Button variant="secondary" size="sm" className="flex items-center gap-2">
              <ArrowLeftIcon className="w-4 h-4" />
              <span className="hidden sm:inline">{t('backToGoals')}</span>
              <span className="sm:hidden">{tCommon('back')}</span>
            </Button>
          </Link>
        </div>

        {/* Form Card */}
        <div className="max-w-2xl mx-auto">
          <Card className="rounded-[26px] border border-violet-100/70 shadow-[0_20px_46px_-30px_rgba(76,29,149,0.45)] backdrop-blur-xs bg-white/92">
            <CardHeader>
              <CardTitle className="font-[var(--font-dash-sans)] text-lg font-semibold text-slate-900">{tCommon('edit')} - {name}</CardTitle>
            </CardHeader>
            <CardContent>
              <form onSubmit={handleSubmit} className="space-y-6">
                <div className="space-y-2">
                  <Label htmlFor="name">{t('form.name')} *</Label>
                  <Input
                    id="name"
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    placeholder={t('form.namePlaceholder')}
                    required
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="description">{t('form.description')}</Label>
                  <Textarea
                    id="description"
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                    placeholder={t('form.descriptionPlaceholder')}
                    rows={3}
                  />
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="targetAmount">{t('form.targetAmount')} *</Label>
                    <Input
                      id="targetAmount"
                      type="number"
                      min="0.01"
                      step="0.01"
                      value={targetAmount}
                      onChange={(e) => setTargetAmount(e.target.value)}
                      placeholder="0.00"
                      required
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="currentAmount">{t('form.currentAmount')}</Label>
                    <Input
                      id="currentAmount"
                      type="number"
                      min="0"
                      step="0.01"
                      value={currentAmount}
                      onChange={(e) => setCurrentAmount(e.target.value)}
                      placeholder="0.00"
                      disabled={!!linkedAccountId}
                    />
                    {linkedAccountId && (
                      <p className="text-xs text-slate-500">{t('form.currentAmountLinkedHint')}</p>
                    )}
                  </div>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="deadline">{t('form.deadline')}</Label>
                  <Input
                    id="deadline"
                    type="date"
                    value={deadline}
                    onChange={(e) => setDeadline(e.target.value)}
                  />
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="goalType">{t('form.goalType')}</Label>
                    <Select
                      id="goalType"
                      value={goalType}
                      onChange={(e) => setGoalType(e.target.value)}
                      className="w-full"
                    >
                      {GOAL_TYPES.map((type) => (
                        <option key={type} value={type}>
                          {t(`goalTypes.${type}`)}
                        </option>
                      ))}
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="status">{t('form.status')}</Label>
                    <Select
                      id="status"
                      value={status}
                      onChange={(e) => setStatus(e.target.value)}
                      className="w-full"
                    >
                      {STATUSES.map((s) => (
                        <option key={s} value={s}>
                          {t(`statuses.${s}`)}
                        </option>
                      ))}
                    </Select>
                  </div>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="linkedAccount">{t('form.linkedAccount')}</Label>
                  <Select
                    id="linkedAccount"
                    value={linkedAccountId}
                    onChange={(e) => setLinkedAccountId(e.target.value)}
                    className="w-full"
                  >
                    <option value="">{t('form.noLinkedAccount')}</option>
                    {accounts.map((account) => (
                      <option key={account.id} value={account.id}>
                        {account.name}
                      </option>
                    ))}
                  </Select>
                  <p className="text-xs text-slate-500">{t('form.linkedAccountHelp')}</p>
                </div>

                <div className="flex justify-end gap-3">
                  <Link href={`/goals/${goalId}`}>
                    <Button type="button" variant="secondary">
                      {tCommon('cancel')}
                    </Button>
                  </Link>
                  <Button type="submit" disabled={!canSubmit || isSubmitting}>
                    {isSubmitting ? tCommon('saving') : tCommon('save')}
                  </Button>
                </div>
              </form>
            </CardContent>
          </Card>
        </div>
    </AppLayout>
  );
}
