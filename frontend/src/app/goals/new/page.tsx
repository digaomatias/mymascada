'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import Link from 'next/link';
import Navigation from '@/components/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select } from '@/components/ui/select';
import { apiClient, CreateGoalRequest } from '@/lib/api-client';
import { toast } from 'sonner';
import { ArrowLeftIcon } from '@heroicons/react/24/outline';

interface Account {
  id: number;
  name: string;
}

const GOAL_TYPES = ['EmergencyFund', 'Savings', 'DebtPayoff', 'Investment', 'Custom'] as const;

export default function CreateGoalPage() {
  const router = useRouter();
  const t = useTranslations('goals');
  const tCommon = useTranslations('common');

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [accounts, setAccounts] = useState<Account[]>([]);

  // Form state
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [targetAmount, setTargetAmount] = useState('');
  const [deadline, setDeadline] = useState('');
  const [goalType, setGoalType] = useState('Savings');
  const [linkedAccountId, setLinkedAccountId] = useState('');

  useEffect(() => {
    const loadAccounts = async () => {
      try {
        const data = (await apiClient.getAccounts()) as Account[];
        setAccounts(data);
      } catch {
        // Accounts are optional, don't show error
      }
    };
    loadAccounts();
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    const amount = parseFloat(targetAmount);
    if (!name.trim() || isNaN(amount) || amount <= 0) {
      return;
    }

    try {
      setIsSubmitting(true);
      const request: CreateGoalRequest = {
        name: name.trim(),
        description: description.trim() || undefined,
        targetAmount: amount,
        deadline: deadline || undefined,
        goalType,
        linkedAccountId: linkedAccountId ? parseInt(linkedAccountId) : undefined,
      };

      await apiClient.createGoal(request);
      toast.success(t('goalCreated'));
      router.push('/goals');
    } catch {
      toast.error(t('createError'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const canSubmit = name.trim() !== '' && parseFloat(targetAmount) > 0;

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
      <Navigation />

      <main className="container mx-auto px-4 py-4 sm:py-6 lg:py-8">
        {/* Navigation Bar */}
        <div className="flex items-center justify-between mb-6">
          <Link href="/goals">
            <Button variant="secondary" size="sm" className="flex items-center gap-2">
              <ArrowLeftIcon className="w-4 h-4" />
              <span className="hidden sm:inline">{t('backToGoals')}</span>
              <span className="sm:hidden">{tCommon('back')}</span>
            </Button>
          </Link>
        </div>

        {/* Form Card */}
        <div className="max-w-2xl mx-auto">
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardHeader>
              <CardTitle>{t('createGoal')}</CardTitle>
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
                  <Label htmlFor="deadline">{t('form.deadline')}</Label>
                  <Input
                    id="deadline"
                    type="date"
                    value={deadline}
                    onChange={(e) => setDeadline(e.target.value)}
                  />
                </div>

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
                  <p className="text-xs text-gray-500">{t('form.linkedAccountHelp')}</p>
                </div>

                <div className="flex justify-end gap-3">
                  <Link href="/goals">
                    <Button type="button" variant="secondary">
                      {tCommon('cancel')}
                    </Button>
                  </Link>
                  <Button type="submit" disabled={!canSubmit || isSubmitting}>
                    {isSubmitting ? tCommon('creating') : tCommon('create')}
                  </Button>
                </div>
              </form>
            </CardContent>
          </Card>
        </div>
      </main>
    </div>
  );
}
