'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { AppLayout } from '@/components/app-layout';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Badge } from '@/components/ui/badge';
import { apiClient } from '@/lib/api-client';
import type {
  BudgetPeriodType,
  BudgetSuggestion,
  CreateBudgetRequest,
  CreateBudgetCategoryRequest,
} from '@/types/budget';
import { formatCurrency } from '@/types/budget';
import { toast } from 'sonner';
import {
  ArrowLeftIcon,
  LightBulbIcon,
  MagnifyingGlassIcon,
  PlusIcon,
  TrashIcon,
} from '@heroicons/react/24/outline';
import { renderCategoryIcon } from '@/lib/category-icons';
import { BudgetWizardStepShell } from '@/components/budget/budget-wizard-step-shell';

const BUDGET_BASE = '/budgets';

interface Category {
  id: number;
  name: string;
  icon?: string;
}

interface CategorySelection extends CreateBudgetCategoryRequest {
  categoryName: string;
  categoryIcon?: string;
  suggestion?: BudgetSuggestion;
}

function toISODate(date: Date) {
  return date.toISOString().split('T')[0];
}

export default function CreateBudgetPage() {
  const router = useRouter();
  const t = useTranslations('budgets');
  const tCommon = useTranslations('common');
  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [submitting, setSubmitting] = useState(false);
  const [loadingCategories, setLoadingCategories] = useState(true);

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [periodType, setPeriodType] = useState<BudgetPeriodType>('Monthly');
  const [startDate, setStartDate] = useState(() => toISODate(new Date()));
  const [endDate, setEndDate] = useState('');
  const [isRecurring, setIsRecurring] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');

  const [categories, setCategories] = useState<Category[]>([]);
  const [suggestions, setSuggestions] = useState<BudgetSuggestion[]>([]);
  const [selectedCategories, setSelectedCategories] = useState<CategorySelection[]>([]);

  useEffect(() => {
    if (!startDate) return;
    if (periodType === 'Custom') return;

    const start = new Date(startDate);
    const end = new Date(start);
    if (periodType === 'Weekly') {
      end.setDate(end.getDate() + 6);
    } else if (periodType === 'Biweekly') {
      end.setDate(end.getDate() + 13);
    } else {
      end.setMonth(end.getMonth() + 1);
      end.setDate(end.getDate() - 1);
    }
    setEndDate(toISODate(end));
  }, [periodType, startDate]);

  useEffect(() => {
    const load = async () => {
      try {
        setLoadingCategories(true);
        const [categoriesData, suggestionsData] = await Promise.all([
          apiClient.getCategories() as Promise<Category[]>,
          apiClient.getBudgetSuggestions(3).catch(() => [] as BudgetSuggestion[]),
        ]);
        setCategories(categoriesData);
        setSuggestions(suggestionsData);
      } catch {
        toast.error(t('loadError'));
      } finally {
        setLoadingCategories(false);
      }
    };
    load();
  }, [t]);

  const filteredCategories = useMemo(() => {
    return categories.filter((category) => {
      const alreadySelected = selectedCategories.some((selected) => selected.categoryId === category.id);
      return !alreadySelected && category.name.toLowerCase().includes(searchTerm.toLowerCase());
    });
  }, [categories, selectedCategories, searchTerm]);

  const canProceedStep1 = Boolean(name.trim() && startDate && endDate);
  const canProceedStep2 = selectedCategories.length > 0 && selectedCategories.every((item) => item.budgetedAmount > 0);
  const totalBudget = selectedCategories.reduce((sum, item) => sum + item.budgetedAmount, 0);

  const addCategory = (category: Category) => {
    const suggestion = suggestions.find((entry) => entry.categoryId === category.id);
    setSelectedCategories((current) => [
      ...current,
      {
        categoryId: category.id,
        categoryName: category.name,
        categoryIcon: category.icon,
        budgetedAmount: suggestion?.suggestedBudget || 0,
        allowRollover: false,
        includeSubcategories: false,
        suggestion,
      },
    ]);
  };

  const updateCategory = (
    categoryId: number,
    changes: Partial<Pick<CategorySelection, 'budgetedAmount' | 'allowRollover' | 'includeSubcategories'>>,
  ) => {
    setSelectedCategories((current) =>
      current.map((item) => (item.categoryId === categoryId ? { ...item, ...changes } : item)),
    );
  };

  const removeCategory = (categoryId: number) => {
    setSelectedCategories((current) =>
      current.filter((item) => item.categoryId !== categoryId),
    );
  };

  const handleCreate = async () => {
    if (!canProceedStep2 || submitting) {
      return;
    }

    try {
      setSubmitting(true);
      const request: CreateBudgetRequest = {
        name: name.trim(),
        description: description.trim() || undefined,
        periodType,
        startDate,
        endDate: periodType === 'Custom' ? endDate : undefined,
        isRecurring,
        categories: selectedCategories.map((item) => ({
          categoryId: item.categoryId,
          budgetedAmount: item.budgetedAmount,
          allowRollover: item.allowRollover,
          includeSubcategories: item.includeSubcategories,
        })),
      };

      const created = await apiClient.createBudget(request);
      toast.success(t('budgetCreated'));
      router.push(`${BUDGET_BASE}/${created.id}`);
    } catch {
      toast.error(t('createError'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AppLayout>
      <div className="space-y-5">
        <div className="flex items-center justify-between">
          <Link href={BUDGET_BASE}>
            <Button variant="secondary" size="sm" className="flex items-center gap-2">
              <ArrowLeftIcon className="h-4 w-4" />
              {t('backToBudgets')}
            </Button>
          </Link>
        </div>

        {step === 1 ? (
          <BudgetWizardStepShell
            title={t('wizard.step1Title')}
            subtitle={t('wizard.step1Description')}
            step={1}
            backLabel={tCommon('back')}
            nextLabel={tCommon('next')}
            showBack={false}
            onNext={() => setStep(2)}
            nextDisabled={!canProceedStep1}
          >
            <div className="space-y-5">
              <div className="space-y-2">
                <Label htmlFor="budget-name">{t('wizard.budgetName')} *</Label>
                <Input
                  id="budget-name"
                  value={name}
                  onChange={(event) => setName(event.target.value)}
                  placeholder={t('wizard.budgetNamePlaceholder')}
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="budget-description">{t('wizard.budgetDescription')}</Label>
                <Textarea
                  id="budget-description"
                  rows={3}
                  value={description}
                  onChange={(event) => setDescription(event.target.value)}
                  placeholder={t('wizard.budgetDescriptionPlaceholder')}
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="period-type">{t('wizard.periodType')}</Label>
                <Select
                  id="period-type"
                  value={periodType}
                  onChange={(event) => setPeriodType(event.target.value as BudgetPeriodType)}
                >
                  <option value="Monthly">{t('wizard.monthly')}</option>
                  <option value="Weekly">{t('wizard.weekly')}</option>
                  <option value="Biweekly">{t('wizard.biweekly')}</option>
                  <option value="Custom">{t('wizard.custom')}</option>
                </Select>
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="start-date">{t('wizard.startDate')} *</Label>
                  <Input
                    id="start-date"
                    type="date"
                    value={startDate}
                    onChange={(event) => setStartDate(event.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="end-date">{t('wizard.endDate')} *</Label>
                  <Input
                    id="end-date"
                    type="date"
                    value={endDate}
                    disabled={periodType !== 'Custom'}
                    onChange={(event) => setEndDate(event.target.value)}
                  />
                </div>
              </div>

              <label className="flex items-start gap-3 rounded-xl border border-violet-100/80 bg-violet-50/35 p-3">
                <Checkbox
                  checked={isRecurring}
                  onCheckedChange={(checked) => setIsRecurring(checked === true)}
                />
                <div>
                  <p className="text-sm font-medium text-slate-700">{t('wizard.isRecurring')}</p>
                  <p className="text-xs text-slate-500">{t('wizard.isRecurringHelp')}</p>
                </div>
              </label>
            </div>
          </BudgetWizardStepShell>
        ) : null}

        {step === 2 ? (
          <BudgetWizardStepShell
            title={t('wizard.step2Title')}
            subtitle={t('wizard.step2Description')}
            step={2}
            backLabel={tCommon('back')}
            nextLabel={tCommon('next')}
            onBack={() => setStep(1)}
            onNext={() => setStep(3)}
            nextDisabled={!canProceedStep2}
          >
            <div className="space-y-5">
              <div className="space-y-2">
                <Label>{t('wizard.selectCategories')}</Label>
                <div className="relative">
                  <MagnifyingGlassIcon className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                  <Input
                    value={searchTerm}
                    onChange={(event) => setSearchTerm(event.target.value)}
                    placeholder={t('wizard.searchCategories')}
                    className="pl-9"
                  />
                </div>

                {loadingCategories ? (
                  <div className="space-y-2">
                    {[1, 2, 3].map((item) => (
                      <div key={item} className="h-10 animate-pulse rounded-lg bg-violet-100/60" />
                    ))}
                  </div>
                ) : (
                  <div className="max-h-48 overflow-y-auto rounded-xl border border-violet-100/80">
                    {!loadingCategories && filteredCategories.length === 0 && (
                      <p className="px-3 py-3 text-sm text-slate-500">{t('wizard.noMatchingCategories')}</p>
                    )}
                    {filteredCategories.slice(0, 10).map((category) => {
                      const suggestion = suggestions.find((entry) => entry.categoryId === category.id);
                      return (
                        <button
                          key={category.id}
                          type="button"
                          onClick={() => addCategory(category)}
                          className="flex w-full items-center justify-between border-b border-violet-100/70 px-3 py-2 text-left last:border-b-0 hover:bg-violet-50/40"
                        >
                          <span className="inline-flex items-center gap-2 text-sm text-slate-700">
                            {renderCategoryIcon(category.icon, 'h-4 w-4')}
                            {category.name}
                          </span>
                          <span className="inline-flex items-center gap-2 text-xs text-slate-500">
                            {suggestion ? formatCurrency(suggestion.suggestedBudget) : '\u2014'}
                            <PlusIcon className="h-3.5 w-3.5" />
                          </span>
                        </button>
                      );
                    })}
                  </div>
                )}
              </div>

              <div>
                <p className="text-sm font-semibold text-slate-700">
                  {t('wizard.selectedCategories', { count: selectedCategories.length })}
                </p>

                {selectedCategories.length === 0 ? (
                  <p className="mt-2 rounded-xl border border-violet-100/80 bg-violet-50/35 p-3 text-sm text-slate-500">
                    {t('wizard.noCategoriesSelected')}
                  </p>
                ) : (
                  <div className="mt-3 space-y-3">
                    {selectedCategories.map((category) => (
                      <div key={category.categoryId} className="rounded-xl border border-violet-100/80 bg-white p-3">
                        <div className="flex items-start justify-between gap-3">
                          <div>
                            <p className="inline-flex items-center gap-2 text-sm font-semibold text-slate-800">
                              {renderCategoryIcon(category.categoryIcon, 'h-4 w-4')}
                              {category.categoryName}
                            </p>
                            {category.suggestion ? (
                              <button
                                type="button"
                                onClick={() =>
                                  updateCategory(category.categoryId, { budgetedAmount: category.suggestion?.suggestedBudget || 0 })
                                }
                                className="mt-1 inline-flex items-center gap-1 text-xs font-semibold text-violet-600 hover:text-violet-700"
                              >
                                <LightBulbIcon className="h-3.5 w-3.5" />
                                {t('wizard.useSuggestion')} · {formatCurrency(category.suggestion.suggestedBudget)}
                              </button>
                            ) : null}
                          </div>
                          <button
                            type="button"
                            onClick={() => removeCategory(category.categoryId)}
                            className="rounded-md p-1 text-slate-400 hover:bg-violet-50 hover:text-rose-600"
                          >
                            <TrashIcon className="h-4 w-4" />
                          </button>
                        </div>

                        <div className="mt-3 grid gap-3 sm:grid-cols-[140px_1fr] sm:items-center">
                          <Label className="text-xs text-slate-500">{t('wizard.budgetAmount')}</Label>
                          <Input
                            type="number"
                            min="0"
                            step="5"
                            value={category.budgetedAmount || ''}
                            onChange={(event) =>
                              updateCategory(category.categoryId, {
                                budgetedAmount: parseFloat(event.target.value) || 0,
                              })
                            }
                          />
                        </div>

                        <div className="mt-3 flex flex-wrap gap-4">
                          <label className="inline-flex items-center gap-2 text-xs text-slate-600">
                            <Checkbox
                              checked={category.allowRollover}
                              onCheckedChange={(checked) =>
                                updateCategory(category.categoryId, { allowRollover: checked === true })
                              }
                            />
                            {t('wizard.allowRollover')}
                          </label>
                          <label className="inline-flex items-center gap-2 text-xs text-slate-600">
                            <Checkbox
                              checked={category.includeSubcategories}
                              onCheckedChange={(checked) =>
                                updateCategory(category.categoryId, { includeSubcategories: checked === true })
                              }
                            />
                            {t('wizard.includeSubcategories')}
                          </label>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          </BudgetWizardStepShell>
        ) : null}

        {step === 3 ? (
          <BudgetWizardStepShell
            title={t('wizard.step3Title')}
            subtitle={t('wizard.step3Description')}
            step={3}
            backLabel={tCommon('back')}
            nextLabel={t('wizard.createBudget')}
            onBack={() => setStep(2)}
            onNext={handleCreate}
            nextDisabled={!canProceedStep2}
            nextLoading={submitting}
          >
            <div className="space-y-5">
              <section className="grid gap-4 sm:grid-cols-2">
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.1em] text-slate-500">
                    {t('wizard.budgetName')}
                  </p>
                  <p className="mt-1 text-sm font-semibold text-slate-900">{name}</p>
                </div>
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.1em] text-slate-500">
                    {t('wizard.periodType')}
                  </p>
                  <p className="mt-1 text-sm font-semibold text-slate-900">{t(`wizard.${periodType.toLowerCase()}`)}</p>
                </div>
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.1em] text-slate-500">
                    {t('wizard.dateRange')}
                  </p>
                  <p className="mt-1 text-sm font-semibold text-slate-900">
                    {new Date(startDate).toLocaleDateString()} {'\u2014'} {new Date(endDate).toLocaleDateString()}
                  </p>
                </div>
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.1em] text-slate-500">
                    {t('wizard.isRecurring')}
                  </p>
                  <p className="mt-1 text-sm font-semibold text-slate-900">
                    {isRecurring ? tCommon('yes') : tCommon('no')}
                  </p>
                </div>
              </section>

              <section className="rounded-xl border border-violet-100/80 bg-violet-50/35">
                <div className="border-b border-violet-100/80 px-4 py-3">
                  <p className="text-sm font-semibold text-slate-800">{t('wizard.categoriesAndAmounts')}</p>
                </div>
                <div className="divide-y divide-violet-100/80">
                  {selectedCategories.map((category) => (
                    <div key={category.categoryId} className="flex items-center justify-between px-4 py-3">
                      <span className="inline-flex items-center gap-2 text-sm text-slate-700">
                        {renderCategoryIcon(category.categoryIcon, 'h-4 w-4')}
                        {category.categoryName}
                        {category.allowRollover ? (
                          <Badge variant="secondary" className="ml-1 text-[10px]">
                            {t('wizard.allowRollover')}
                          </Badge>
                        ) : null}
                      </span>
                      <span className="text-sm font-semibold text-slate-900">
                        {formatCurrency(category.budgetedAmount)}
                      </span>
                    </div>
                  ))}
                </div>
                <div className="flex items-center justify-between border-t border-violet-100/80 px-4 py-3">
                  <span className="text-sm font-semibold text-slate-700">{t('wizard.totalBudget')}</span>
                  <span className="text-lg font-semibold text-slate-900">{formatCurrency(totalBudget)}</span>
                </div>
              </section>

              <p className="text-xs text-slate-500">{t('wizard.nextAfterCreate')}</p>
            </div>
          </BudgetWizardStepShell>
        ) : null}
      </div>
    </AppLayout>
  );
}
