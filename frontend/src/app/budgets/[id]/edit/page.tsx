'use client';

import { useEffect, useMemo, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { AppLayout } from '@/components/app-layout';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Checkbox } from '@/components/ui/checkbox';
import { Badge } from '@/components/ui/badge';
import { apiClient } from '@/lib/api-client';
import type { BudgetDetail, BudgetSuggestion } from '@/types/budget';
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

const BUDGET_BASE = '/budgets';

interface Category {
  id: number;
  name: string;
  icon?: string;
}

interface CategoryDraft {
  budgetedAmount: number;
  allowRollover: boolean;
  includeSubcategories: boolean;
}

interface NewCategoryDraft extends CategoryDraft {
  categoryId: number;
  categoryName: string;
  categoryIcon?: string;
  suggestion?: BudgetSuggestion;
}

export default function EditBudgetPage() {
  const params = useParams();
  const router = useRouter();
  const t = useTranslations('budgets');
  const tCommon = useTranslations('common');
  const budgetId = Number(params.id);

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [budget, setBudget] = useState<BudgetDetail | null>(null);
  const [categories, setCategories] = useState<Category[]>([]);
  const [suggestions, setSuggestions] = useState<BudgetSuggestion[]>([]);
  const [searchTerm, setSearchTerm] = useState('');

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [isRecurring, setIsRecurring] = useState(true);

  const [categoryAllocations, setCategoryAllocations] = useState<Record<number, CategoryDraft>>({});
  const [removedCategoryIds, setRemovedCategoryIds] = useState<number[]>([]);
  const [newCategories, setNewCategories] = useState<NewCategoryDraft[]>([]);

  useEffect(() => {
    if (isNaN(budgetId)) {
      router.push(BUDGET_BASE);
      return;
    }

    const load = async () => {
      try {
        setLoading(true);
        const [budgetData, categoriesData, suggestionsData] = await Promise.all([
          apiClient.getBudget(budgetId),
          apiClient.getCategories() as Promise<Category[]>,
          apiClient.getBudgetSuggestions(3).catch(() => [] as BudgetSuggestion[]),
        ]);

        setBudget(budgetData);
        setCategories(categoriesData);
        setSuggestions(suggestionsData);
        setName(budgetData.name);
        setDescription(budgetData.description || '');
        setIsActive(budgetData.isActive);
        setIsRecurring(budgetData.isRecurring);

        const initialAllocations: Record<number, CategoryDraft> = {};
        budgetData.categories.forEach((category) => {
          initialAllocations[category.categoryId] = {
            budgetedAmount: category.budgetedAmount,
            allowRollover: category.allowRollover,
            includeSubcategories: category.includeSubcategories,
          };
        });
        setCategoryAllocations(initialAllocations);
      } catch {
        toast.error(t('loadError'));
        router.push(BUDGET_BASE);
      } finally {
        setLoading(false);
      }
    };

    load();
  }, [budgetId, router, t]);

  const selectedCategoryIds = useMemo(() => {
    return new Set([
      ...Object.keys(categoryAllocations).map(Number),
      ...newCategories.map((entry) => entry.categoryId),
    ]);
  }, [categoryAllocations, newCategories]);

  const filteredCategories = useMemo(() => {
    return categories.filter((category) => {
      return (
        !selectedCategoryIds.has(category.id) &&
        category.name.toLowerCase().includes(searchTerm.toLowerCase())
      );
    });
  }, [categories, searchTerm, selectedCategoryIds]);

  const existingCategoryMeta = useMemo(() => {
    const map = new Map<number, { name: string; icon?: string }>();
    budget?.categories.forEach((category) => {
      map.set(category.categoryId, {
        name: category.categoryName,
        icon: category.categoryIcon,
      });
    });
    return map;
  }, [budget]);

  const handleExistingCategoryUpdate = (categoryId: number, updates: Partial<CategoryDraft>) => {
    setCategoryAllocations((current) => ({
      ...current,
      [categoryId]: {
        ...current[categoryId],
        ...updates,
      },
    }));
  };

  const handleRemoveExistingCategory = (categoryId: number) => {
    setRemovedCategoryIds((current) => [...new Set([...current, categoryId])]);
    setCategoryAllocations((current) => {
      const next = { ...current };
      delete next[categoryId];
      return next;
    });
  };

  const handleAddCategory = (category: Category) => {
    const suggestion = suggestions.find((entry) => entry.categoryId === category.id);
    setNewCategories((current) => [
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

  const handleUpdateNewCategory = (categoryId: number, updates: Partial<CategoryDraft>) => {
    setNewCategories((current) =>
      current.map((entry) =>
        entry.categoryId === categoryId ? { ...entry, ...updates } : entry,
      ),
    );
  };

  const handleRemoveNewCategory = (categoryId: number) => {
    setNewCategories((current) =>
      current.filter((entry) => entry.categoryId !== categoryId),
    );
  };

  const totalBudget = useMemo(() => {
    const existing = Object.values(categoryAllocations).reduce(
      (sum, entry) => sum + entry.budgetedAmount,
      0,
    );
    const added = newCategories.reduce((sum, entry) => sum + entry.budgetedAmount, 0);
    return existing + added;
  }, [categoryAllocations, newCategories]);

  const handleSave = async () => {
    if (!budget || !name.trim()) return;

    setSaving(true);
    try {
      await apiClient.updateBudget(budget.id, {
        name: name.trim(),
        description: description.trim() || undefined,
        isActive,
        isRecurring,
      });
    } catch {
      toast.error(t('updateError'));
      setSaving(false);
      return;
    }

    for (const categoryId of removedCategoryIds) {
      try {
        await apiClient.removeBudgetCategory(budget.id, categoryId);
      } catch {
        const meta = existingCategoryMeta.get(categoryId);
        toast.error(t('removeCategoryError', { name: meta?.name || String(categoryId) }));
      }
    }

    for (const [categoryId, allocation] of Object.entries(categoryAllocations)) {
      try {
        await apiClient.updateBudgetCategory(budget.id, Number(categoryId), {
          budgetedAmount: allocation.budgetedAmount,
          allowRollover: allocation.allowRollover,
          includeSubcategories: allocation.includeSubcategories,
        });
      } catch {
        const meta = existingCategoryMeta.get(Number(categoryId));
        toast.error(t('updateCategoryError', { name: meta?.name || categoryId }));
      }
    }

    for (const category of newCategories) {
      try {
        await apiClient.addBudgetCategory(budget.id, {
          categoryId: category.categoryId,
          budgetedAmount: category.budgetedAmount,
          allowRollover: category.allowRollover,
          includeSubcategories: category.includeSubcategories,
        });
      } catch {
        toast.error(t('addCategoryError', { name: category.categoryName }));
      }
    }

    toast.success(t('budgetUpdated'));
    setSaving(false);
    router.push(`${BUDGET_BASE}/${budget.id}`);
  };

  if (loading) {
    return (
      <AppLayout>
        <div className="space-y-4">
          <div className="h-24 animate-pulse rounded-[24px] border border-violet-100/80 bg-white/80" />
          <div className="h-96 animate-pulse rounded-[24px] border border-violet-100/80 bg-white/80" />
        </div>
      </AppLayout>
    );
  }

  if (!budget) return null;

  return (
    <AppLayout>
      <div>
        <header className="mb-5">
          <Link
            href={`${BUDGET_BASE}/${budget.id}`}
            className="inline-flex items-center text-sm font-medium text-slate-500 transition-colors hover:text-violet-700"
          >
            <ArrowLeftIcon className="mr-1.5 h-4 w-4" />
            {tCommon('back')}
          </Link>
          <h1 className="mt-2 font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900 sm:text-[2.1rem]">
            {t('editBudget')}
          </h1>
          <p className="mt-1.5 text-[15px] text-slate-500">{t('edit.subtitle')}</p>
        </header>

        <div className="space-y-5">
        <section className="space-y-4 rounded-[28px] border border-violet-100/80 bg-white/92 p-6 shadow-[0_20px_42px_-30px_rgba(76,29,149,0.45)]">
          <div>
            <h2 className="text-lg font-semibold tracking-[-0.02em] text-slate-900">
              {t('edit.basicInfo')}
            </h2>
          </div>

          <div className="space-y-2">
            <Label htmlFor="edit-name">{t('wizard.budgetName')} *</Label>
            <Input
              id="edit-name"
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder={t('wizard.budgetNamePlaceholder')}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="edit-description">{t('wizard.budgetDescription')}</Label>
            <Textarea
              id="edit-description"
              rows={3}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder={t('wizard.budgetDescriptionPlaceholder')}
            />
          </div>

          <div className="flex flex-wrap gap-4">
            <label className="inline-flex items-center gap-2 text-sm text-slate-600">
              <Checkbox checked={isActive} onCheckedChange={(checked) => setIsActive(checked === true)} />
              {t('active')}
            </label>
            <label className="inline-flex items-center gap-2 text-sm text-slate-600">
              <Checkbox checked={isRecurring} onCheckedChange={(checked) => setIsRecurring(checked === true)} />
              {t('wizard.isRecurring')}
            </label>
          </div>
        </section>

        <section className="space-y-4 rounded-[28px] border border-violet-100/80 bg-white/92 p-6 shadow-[0_20px_42px_-30px_rgba(76,29,149,0.45)]">
          <h2 className="text-lg font-semibold tracking-[-0.02em] text-slate-900">
            {t('edit.categoryAllocations')}
          </h2>

          <div className="space-y-3">
            {Object.entries(categoryAllocations).map(([id, allocation]) => {
              const categoryId = Number(id);
              const meta = existingCategoryMeta.get(categoryId);
              return (
                <div key={id} className="rounded-xl border border-violet-100/80 p-4">
                  <div className="flex items-start justify-between gap-3">
                    <p className="inline-flex items-center gap-2 text-sm font-semibold text-slate-800">
                      {renderCategoryIcon(meta?.icon, 'h-4 w-4')}
                      {meta?.name || id}
                    </p>
                    <button
                      type="button"
                      onClick={() => handleRemoveExistingCategory(categoryId)}
                      className="rounded-md p-1 text-slate-400 hover:bg-violet-50 hover:text-rose-600"
                    >
                      <TrashIcon className="h-4 w-4" />
                    </button>
                  </div>

                  <div className="mt-3 space-y-3">
                    <div className="grid gap-3 sm:grid-cols-[140px_1fr] sm:items-center">
                      <Label className="text-xs text-slate-500">{t('wizard.budgetAmount')}</Label>
                      <Input
                        type="number"
                        min="0"
                        step="5"
                        value={allocation.budgetedAmount || ''}
                        onChange={(event) =>
                          handleExistingCategoryUpdate(categoryId, {
                            budgetedAmount: parseFloat(event.target.value) || 0,
                          })
                        }
                      />
                    </div>
                    <div className="flex flex-wrap gap-4">
                      <label className="inline-flex items-center gap-2 text-xs text-slate-600">
                        <Checkbox
                          checked={allocation.allowRollover}
                          onCheckedChange={(checked) =>
                            handleExistingCategoryUpdate(categoryId, {
                              allowRollover: checked === true,
                            })
                          }
                        />
                        {t('wizard.allowRollover')}
                      </label>
                      <label className="inline-flex items-center gap-2 text-xs text-slate-600">
                        <Checkbox
                          checked={allocation.includeSubcategories}
                          onCheckedChange={(checked) =>
                            handleExistingCategoryUpdate(categoryId, {
                              includeSubcategories: checked === true,
                            })
                          }
                        />
                        {t('wizard.includeSubcategories')}
                      </label>
                    </div>
                  </div>
                </div>
              );
            })}

            {newCategories.map((category) => (
              <div key={category.categoryId} className="rounded-xl border border-violet-100/80 p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="inline-flex items-center gap-2 text-sm font-semibold text-slate-800">
                      {renderCategoryIcon(category.categoryIcon, 'h-4 w-4')}
                      {category.categoryName}
                      <Badge variant="secondary" className="text-[10px]">{t('edit.newBadge')}</Badge>
                    </p>
                    {category.suggestion ? (
                      <button
                        type="button"
                        onClick={() =>
                          handleUpdateNewCategory(category.categoryId, { budgetedAmount: category.suggestion?.suggestedBudget || 0 })
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
                    onClick={() => handleRemoveNewCategory(category.categoryId)}
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
                      handleUpdateNewCategory(category.categoryId, {
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
                        handleUpdateNewCategory(category.categoryId, { allowRollover: checked === true })
                      }
                    />
                    {t('wizard.allowRollover')}
                  </label>
                  <label className="inline-flex items-center gap-2 text-xs text-slate-600">
                    <Checkbox
                      checked={category.includeSubcategories}
                      onCheckedChange={(checked) =>
                        handleUpdateNewCategory(category.categoryId, { includeSubcategories: checked === true })
                      }
                    />
                    {t('wizard.includeSubcategories')}
                  </label>
                </div>
              </div>
            ))}
          </div>
        </section>

        <section className="space-y-3 rounded-[28px] border border-violet-100/80 bg-white/92 p-6 shadow-[0_20px_42px_-30px_rgba(76,29,149,0.45)]">
          <h2 className="text-lg font-semibold tracking-[-0.02em] text-slate-900">
            {t('edit.addMoreCategories')}
          </h2>
          <div className="relative">
            <MagnifyingGlassIcon className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <Input
              value={searchTerm}
              onChange={(event) => setSearchTerm(event.target.value)}
              placeholder={t('wizard.searchCategories')}
              className="pl-9"
            />
          </div>
          <div className="max-h-56 overflow-y-auto rounded-xl border border-violet-100/80">
            {filteredCategories.slice(0, 15).map((category) => (
              <button
                key={category.id}
                type="button"
                onClick={() => handleAddCategory(category)}
                className="flex w-full items-center justify-between border-b border-violet-100/70 px-3 py-2 text-left last:border-b-0 hover:bg-violet-50/40"
              >
                <span className="inline-flex items-center gap-2 text-sm text-slate-700">
                  {renderCategoryIcon(category.icon, 'h-4 w-4')}
                  {category.name}
                </span>
                <span className="inline-flex items-center gap-2 text-xs text-slate-500">
                  <PlusIcon className="h-3.5 w-3.5" />
                </span>
              </button>
            ))}
            {filteredCategories.length === 0 ? (
              <p className="px-3 py-3 text-sm text-slate-500">{t('wizard.noMatchingCategories')}</p>
            ) : null}
          </div>
        </section>

        <footer className="sticky bottom-4 z-20 flex items-center justify-between rounded-2xl border border-violet-200/80 bg-white/95 p-4 shadow-[0_18px_36px_-24px_rgba(76,29,149,0.5)] backdrop-blur">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.1em] text-slate-500">{t('wizard.totalBudget')}</p>
            <p className="text-xl font-semibold tracking-[-0.02em] text-slate-900">{formatCurrency(totalBudget)}</p>
          </div>
          <div className="flex items-center gap-2">
            <Link href={`${BUDGET_BASE}/${budget.id}`}>
              <Button variant="outline">{tCommon('cancel')}</Button>
            </Link>
            <Button onClick={handleSave} disabled={saving || !name.trim()} loading={saving}>
              {tCommon('save')}
            </Button>
          </div>
        </footer>
        </div>
      </div>
    </AppLayout>
  );
}
