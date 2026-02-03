'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import Link from 'next/link';
import Navigation from '@/components/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Checkbox } from '@/components/ui/checkbox';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { apiClient } from '@/lib/api-client';
import {
  BudgetDetail,
  BudgetSuggestion,
  UpdateBudgetRequest,
  CreateBudgetCategoryRequest,
  UpdateBudgetCategoryRequest,
  formatCurrency,
} from '@/types/budget';
import { toast } from 'sonner';

interface Category {
  id: number;
  name: string;
  canonicalKey?: string;
  type: number;
  parentId: number | null;
  color?: string;
  icon?: string;
  isSystem?: boolean;
  children?: Category[];
}
import {
  ArrowLeft,
  Lightbulb,
  Plus,
  Search,
  Trash2,
  Wallet,
} from 'lucide-react';
import { renderCategoryIcon } from '@/lib/category-icons';

export default function EditBudgetPage() {
  const params = useParams();
  const router = useRouter();
  const t = useTranslations('budgets');
  const tCommon = useTranslations('common');

  const budgetId = Number(params.id);

  const [budget, setBudget] = useState<BudgetDetail | null>(null);
  const [categories, setCategories] = useState<Category[]>([]);
  const [suggestions, setSuggestions] = useState<BudgetSuggestion[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');

  // Form state
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [isRecurring, setIsRecurring] = useState(true);

  // Category allocations (for existing categories)
  const [categoryAllocations, setCategoryAllocations] = useState<Record<number, {
    budgetedAmount: number;
    allowRollover: boolean;
    includeSubcategories: boolean;
  }>>({});

  // New categories to add
  const [newCategories, setNewCategories] = useState<{
    categoryId: number;
    categoryName: string;
    categoryIcon?: string;
    budgetedAmount: number;
    allowRollover: boolean;
    includeSubcategories: boolean;
    suggestion?: BudgetSuggestion;
  }[]>([]);

  useEffect(() => {
    const loadData = async () => {
      try {
        setIsLoading(true);
        const [budgetData, categoriesData, suggestionsData] = await Promise.all([
          apiClient.getBudget(budgetId),
          apiClient.getCategories() as Promise<Category[]>,
          apiClient.getBudgetSuggestions(3).catch(() => [] as BudgetSuggestion[]),
        ]);

        setBudget(budgetData);
        setCategories(categoriesData as Category[]);
        setSuggestions(suggestionsData);

        // Initialize form state
        setName(budgetData.name);
        setDescription(budgetData.description || '');
        setIsActive(budgetData.isActive);
        setIsRecurring(budgetData.isRecurring);

        // Initialize category allocations
        const allocations: Record<number, {
          budgetedAmount: number;
          allowRollover: boolean;
          includeSubcategories: boolean;
        }> = {};
        budgetData.categories.forEach((cat) => {
          allocations[cat.categoryId] = {
            budgetedAmount: cat.budgetedAmount,
            allowRollover: cat.allowRollover,
            includeSubcategories: cat.includeSubcategories,
          };
        });
        setCategoryAllocations(allocations);
      } catch {
        toast.error(t('loadError'));
        router.push('/budgets');
      } finally {
        setIsLoading(false);
      }
    };

    if (budgetId) {
      loadData();
    }
  }, [budgetId]);

  const existingCategoryIds = budget?.categories.map((c) => c.categoryId) || [];
  const newCategoryIds = newCategories.map((c) => c.categoryId);
  const allSelectedCategoryIds = [...existingCategoryIds, ...newCategoryIds];

  const filteredCategories = categories.filter(
    (cat) =>
      cat.name.toLowerCase().includes(searchTerm.toLowerCase()) &&
      !allSelectedCategoryIds.includes(cat.id)
  );

  const handleAddCategory = (category: Category) => {
    const suggestion = suggestions.find((s) => s.categoryId === category.id);
    setNewCategories([
      ...newCategories,
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

  const handleRemoveNewCategory = (categoryId: number) => {
    setNewCategories(newCategories.filter((c) => c.categoryId !== categoryId));
  };

  const handleUpdateNewCategory = (categoryId: number, updates: Partial<{
    budgetedAmount: number;
    allowRollover: boolean;
    includeSubcategories: boolean;
  }>) => {
    setNewCategories(
      newCategories.map((c) =>
        c.categoryId === categoryId ? { ...c, ...updates } : c
      )
    );
  };

  const handleUpdateExistingCategory = (categoryId: number, updates: Partial<{
    budgetedAmount: number;
    allowRollover: boolean;
    includeSubcategories: boolean;
  }>) => {
    setCategoryAllocations((prev) => ({
      ...prev,
      [categoryId]: { ...prev[categoryId], ...updates },
    }));
  };

  const handleRemoveExistingCategory = async (categoryId: number) => {
    const category = budget?.categories.find((c) => c.categoryId === categoryId);
    if (!category || !confirm(t('removeCategoryConfirm', { category: category.categoryName }))) {
      return;
    }

    try {
      await apiClient.removeBudgetCategory(budgetId, categoryId);
      toast.success(t('categoryRemoved'));
      // Refresh budget data
      const updatedBudget = await apiClient.getBudget(budgetId);
      setBudget(updatedBudget);
      const newAllocations = { ...categoryAllocations };
      delete newAllocations[categoryId];
      setCategoryAllocations(newAllocations);
    } catch {
      toast.error(t('deleteError'));
    }
  };

  const handleSave = async () => {
    try {
      setIsSaving(true);

      // Update budget basic info
      const updateRequest: UpdateBudgetRequest = {
        name,
        description: description || undefined,
        isActive,
        isRecurring,
      };
      await apiClient.updateBudget(budgetId, updateRequest);

      // Update existing category allocations
      for (const category of budget?.categories || []) {
        const allocation = categoryAllocations[category.categoryId];
        if (allocation) {
          const update: UpdateBudgetCategoryRequest = {
            budgetedAmount: allocation.budgetedAmount,
            allowRollover: allocation.allowRollover,
            includeSubcategories: allocation.includeSubcategories,
          };
          await apiClient.updateBudgetCategory(budgetId, category.categoryId, update);
        }
      }

      // Add new categories
      for (const newCat of newCategories) {
        const request: CreateBudgetCategoryRequest = {
          categoryId: newCat.categoryId,
          budgetedAmount: newCat.budgetedAmount,
          allowRollover: newCat.allowRollover,
          includeSubcategories: newCat.includeSubcategories,
        };
        await apiClient.addBudgetCategory(budgetId, request);
      }

      toast.success(t('budgetUpdated'));
      router.push(`/budgets/${budgetId}`);
    } catch {
      toast.error(t('updateError'));
    } finally {
      setIsSaving(false);
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
        <Navigation />
        <main className="container mx-auto px-4 py-6 max-w-3xl space-y-6">
          <Skeleton className="h-8 w-48" />
          <Skeleton className="h-64" />
          <Skeleton className="h-96" />
        </main>
      </div>
    );
  }

  if (!budget) {
    return null;
  }

  const totalBudget =
    Object.values(categoryAllocations).reduce((sum, a) => sum + a.budgetedAmount, 0) +
    newCategories.reduce((sum, c) => sum + c.budgetedAmount, 0);

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
      <Navigation />
      <main className="container mx-auto px-4 py-6 max-w-3xl space-y-6">
      {/* Header */}
      <div>
        <Link href={`/budgets/${budgetId}`}>
          <Button variant="ghost" size="sm" className="-ml-2 mb-2">
            <ArrowLeft className="h-4 w-4 mr-1" />
            {tCommon('back')}
          </Button>
        </Link>
        <h1 className="text-2xl font-bold flex items-center gap-2 text-gray-900">
          <Wallet className="h-6 w-6" />
          {t('edit.title')}
        </h1>
      </div>

      {/* Basic Information */}
      <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        <CardHeader>
          <CardTitle>{t('edit.basicInfo')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="name">{t('wizard.budgetName')} *</Label>
            <Input
              id="name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder={t('wizard.budgetNamePlaceholder')}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="description">{t('wizard.budgetDescription')}</Label>
            <Textarea
              id="description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder={t('wizard.budgetDescriptionPlaceholder')}
              rows={3}
            />
          </div>

          <div className="flex items-center gap-6">
            <div className="flex items-center space-x-2">
              <Checkbox
                id="isActive"
                checked={isActive}
                onCheckedChange={(checked) => setIsActive(checked === true)}
              />
              <Label htmlFor="isActive">{t('active')}</Label>
            </div>
            <div className="flex items-center space-x-2">
              <Checkbox
                id="isRecurring"
                checked={isRecurring}
                onCheckedChange={(checked) => setIsRecurring(checked === true)}
              />
              <Label htmlFor="isRecurring">{t('wizard.isRecurring')}</Label>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Category Allocations */}
      <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        <CardHeader>
          <CardTitle>{t('edit.categoryAllocations')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Existing categories */}
          {budget.categories.map((category) => {
            const allocation = categoryAllocations[category.categoryId];
            if (!allocation) return null;

            return (
              <div key={category.categoryId} className="border rounded-lg p-4 space-y-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    {renderCategoryIcon(category.categoryIcon, 'h-4 w-4')}
                    <span className="font-medium">{category.categoryName}</span>
                  </div>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => handleRemoveExistingCategory(category.categoryId)}
                    className="text-destructive hover:text-destructive"
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </div>

                <div className="space-y-2">
                  <Label className="text-sm">{t('wizard.budgetAmount')}</Label>
                  <Input
                    type="number"
                    min="0"
                    step="10"
                    value={allocation.budgetedAmount || ''}
                    onChange={(e) =>
                      handleUpdateExistingCategory(category.categoryId, {
                        budgetedAmount: parseFloat(e.target.value) || 0,
                      })
                    }
                  />
                </div>

                <div className="flex items-center gap-6">
                  <div className="flex items-center space-x-2">
                    <Checkbox
                      id={`rollover-${category.categoryId}`}
                      checked={allocation.allowRollover}
                      onCheckedChange={(checked) =>
                        handleUpdateExistingCategory(category.categoryId, {
                          allowRollover: checked === true,
                        })
                      }
                    />
                    <Label htmlFor={`rollover-${category.categoryId}`} className="text-sm">
                      {t('wizard.allowRollover')}
                    </Label>
                  </div>
                  <div className="flex items-center space-x-2">
                    <Checkbox
                      id={`subcategories-${category.categoryId}`}
                      checked={allocation.includeSubcategories}
                      onCheckedChange={(checked) =>
                        handleUpdateExistingCategory(category.categoryId, {
                          includeSubcategories: checked === true,
                        })
                      }
                    />
                    <Label htmlFor={`subcategories-${category.categoryId}`} className="text-sm">
                      {t('wizard.includeSubcategories')}
                    </Label>
                  </div>
                </div>
              </div>
            );
          })}

          {/* New categories */}
          {newCategories.map((newCat) => (
            <div key={newCat.categoryId} className="border rounded-lg p-4 space-y-3 border-green-200 bg-green-50/50">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  {renderCategoryIcon(newCat.categoryIcon, 'h-4 w-4')}
                  <span className="font-medium">{newCat.categoryName}</span>
                  <Badge variant="outline" className="text-xs text-green-600 border-green-600">New</Badge>
                </div>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => handleRemoveNewCategory(newCat.categoryId)}
                  className="text-destructive hover:text-destructive"
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>

              <div className="space-y-2">
                <Label className="text-sm">{t('wizard.budgetAmount')}</Label>
                <div className="flex items-center gap-2">
                  <Input
                    type="number"
                    min="0"
                    step="10"
                    value={newCat.budgetedAmount || ''}
                    onChange={(e) =>
                      handleUpdateNewCategory(newCat.categoryId, {
                        budgetedAmount: parseFloat(e.target.value) || 0,
                      })
                    }
                  />
                  {newCat.suggestion && (
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() =>
                        handleUpdateNewCategory(newCat.categoryId, {
                          budgetedAmount: newCat.suggestion!.suggestedBudget,
                        })
                      }
                      className="whitespace-nowrap"
                    >
                      <Lightbulb className="h-4 w-4 mr-1" />
                      {t('wizard.useSuggestion')}
                    </Button>
                  )}
                </div>
              </div>

              <div className="flex items-center gap-6">
                <div className="flex items-center space-x-2">
                  <Checkbox
                    id={`rollover-new-${newCat.categoryId}`}
                    checked={newCat.allowRollover}
                    onCheckedChange={(checked) =>
                      handleUpdateNewCategory(newCat.categoryId, {
                        allowRollover: checked === true,
                      })
                    }
                  />
                  <Label htmlFor={`rollover-new-${newCat.categoryId}`} className="text-sm">
                    {t('wizard.allowRollover')}
                  </Label>
                </div>
                <div className="flex items-center space-x-2">
                  <Checkbox
                    id={`subcategories-new-${newCat.categoryId}`}
                    checked={newCat.includeSubcategories}
                    onCheckedChange={(checked) =>
                      handleUpdateNewCategory(newCat.categoryId, {
                        includeSubcategories: checked === true,
                      })
                    }
                  />
                  <Label htmlFor={`subcategories-new-${newCat.categoryId}`} className="text-sm">
                    {t('wizard.includeSubcategories')}
                  </Label>
                </div>
              </div>
            </div>
          ))}

          {/* Total */}
          <div className="flex justify-between items-center pt-4 border-t">
            <span className="font-medium">{t('totalBudgeted')}</span>
            <span className="text-xl font-bold">{formatCurrency(totalBudget)}</span>
          </div>
        </CardContent>
      </Card>

      {/* Add More Categories */}
      <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        <CardHeader>
          <CardTitle>{t('edit.addMoreCategories')}</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="relative mb-2">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <Input
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder={t('wizard.searchCategories')}
              className="pl-9"
            />
          </div>
          {filteredCategories.length === 0 ? (
            <p className="text-sm text-muted-foreground py-2">
              {t('wizard.noMatchingCategories')}
            </p>
          ) : (
            <div className="border rounded-md max-h-48 overflow-y-auto">
              {filteredCategories.slice(0, 10).map((category) => {
                const suggestion = suggestions.find((s) => s.categoryId === category.id);
                return (
                  <button
                    key={category.id}
                    onClick={() => handleAddCategory(category)}
                    className="w-full flex items-center justify-between px-3 py-2 hover:bg-accent text-left"
                  >
                    <div className="flex items-center gap-2">
                      {renderCategoryIcon(category.icon, 'h-4 w-4')}
                      <span>{category.name}</span>
                    </div>
                    {suggestion && (
                      <Badge variant="secondary" className="text-xs">
                        <Lightbulb className="h-3 w-3 mr-1" />
                        {formatCurrency(suggestion.suggestedBudget)}
                      </Badge>
                    )}
                    <Plus className="h-4 w-4 text-muted-foreground" />
                  </button>
                );
              })}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Save Button */}
      <div className="flex justify-end gap-4">
        <Link href={`/budgets/${budgetId}`}>
          <Button variant="outline">{tCommon('cancel')}</Button>
        </Link>
        <Button onClick={handleSave} disabled={isSaving || !name.trim()}>
          {isSaving ? t('edit.saving') : t('edit.saveChanges')}
        </Button>
      </div>
      </main>
    </div>
  );
}
