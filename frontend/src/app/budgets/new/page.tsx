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
import { Checkbox } from '@/components/ui/checkbox';
import { Select } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { apiClient } from '@/lib/api-client';
import { renderCategoryIcon } from '@/lib/category-icons';
import {
  BudgetSuggestion,
  CreateBudgetRequest,
  CreateBudgetCategoryRequest,
  BudgetPeriodType,
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
  ArrowRight,
  Check,
  Lightbulb,
  Plus,
  Search,
  Trash2,
} from 'lucide-react';
import { ArrowLeftIcon } from '@heroicons/react/24/outline';
import { cn } from '@/lib/utils';

interface CategorySelection extends CreateBudgetCategoryRequest {
  categoryName: string;
  categoryIcon?: string;
  suggestion?: BudgetSuggestion;
}

export default function CreateBudgetPage() {
  const router = useRouter();
  const t = useTranslations('budgets');
  const tCommon = useTranslations('common');

  // Wizard state
  const [step, setStep] = useState(1);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Step 1: Basic info
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [periodType, setPeriodType] = useState<BudgetPeriodType>('Monthly');
  const [startDate, setStartDate] = useState(() => {
    const today = new Date();
    return today.toISOString().split('T')[0];
  });
  const [endDate, setEndDate] = useState('');
  const [isRecurring, setIsRecurring] = useState(true);

  // Step 2: Categories
  const [categories, setCategories] = useState<Category[]>([]);
  const [suggestions, setSuggestions] = useState<BudgetSuggestion[]>([]);
  const [selectedCategories, setSelectedCategories] = useState<CategorySelection[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [isLoadingCategories, setIsLoadingCategories] = useState(true);

  // Auto-calculate end date based on period type
  useEffect(() => {
    if (periodType !== 'Custom' && startDate) {
      const start = new Date(startDate);
      let end: Date;

      switch (periodType) {
        case 'Weekly':
          end = new Date(start);
          end.setDate(end.getDate() + 6);
          break;
        case 'Biweekly':
          end = new Date(start);
          end.setDate(end.getDate() + 13);
          break;
        case 'Monthly':
        default:
          end = new Date(start);
          end.setMonth(end.getMonth() + 1);
          end.setDate(end.getDate() - 1);
          break;
      }

      setEndDate(end.toISOString().split('T')[0]);
    }
  }, [periodType, startDate]);

  // Load categories and suggestions
  useEffect(() => {
    const loadData = async () => {
      try {
        setIsLoadingCategories(true);
        const categoriesData = await apiClient.getCategories() as Category[];
        setCategories(categoriesData);
      } catch {
        toast.error('Failed to load categories');
      } finally {
        setIsLoadingCategories(false);
      }

      try {
        const suggestionsData = await apiClient.getBudgetSuggestions(3);
        setSuggestions(suggestionsData);
      } catch {
        // Suggestions are optional, don't show error
      }
    };

    loadData();
  }, []);

  // Filter categories for search
  const filteredCategories = categories.filter(
    (cat) =>
      cat.name.toLowerCase().includes(searchTerm.toLowerCase()) &&
      !selectedCategories.some((sc) => sc.categoryId === cat.id)
  );

  const handleAddCategory = (category: Category) => {
    const suggestion = suggestions.find((s) => s.categoryId === category.id);
    setSelectedCategories([
      ...selectedCategories,
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

  const handleRemoveCategory = (categoryId: number) => {
    setSelectedCategories(selectedCategories.filter((c) => c.categoryId !== categoryId));
  };

  const handleUpdateCategoryAmount = (categoryId: number, amount: number) => {
    setSelectedCategories(
      selectedCategories.map((c) =>
        c.categoryId === categoryId ? { ...c, budgetedAmount: amount } : c
      )
    );
  };

  const handleUseSuggestion = (categoryId: number) => {
    const selection = selectedCategories.find((c) => c.categoryId === categoryId);
    if (selection?.suggestion) {
      handleUpdateCategoryAmount(categoryId, selection.suggestion.suggestedBudget);
    }
  };

  const handleToggleRollover = (categoryId: number, checked: boolean) => {
    setSelectedCategories(
      selectedCategories.map((c) =>
        c.categoryId === categoryId ? { ...c, allowRollover: checked } : c
      )
    );
  };

  const handleToggleSubcategories = (categoryId: number, checked: boolean) => {
    setSelectedCategories(
      selectedCategories.map((c) =>
        c.categoryId === categoryId ? { ...c, includeSubcategories: checked } : c
      )
    );
  };

  const handleSubmit = async () => {
    if (selectedCategories.length === 0) {
      toast.error(t('wizard.selectCategoryFirst'));
      return;
    }

    try {
      setIsSubmitting(true);
      const request: CreateBudgetRequest = {
        name,
        description: description || undefined,
        periodType,
        startDate,
        endDate: periodType === 'Custom' ? endDate : undefined,
        isRecurring,
        categories: selectedCategories.map((c) => ({
          categoryId: c.categoryId,
          budgetedAmount: c.budgetedAmount,
          allowRollover: c.allowRollover,
          includeSubcategories: c.includeSubcategories,
        })),
      };

      const created = await apiClient.createBudget(request);
      toast.success(t('budgetCreated'));
      router.push(`/budgets/${created.id}`);
    } catch {
      toast.error(t('createError'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const canProceedStep1 = name.trim() !== '' && startDate && endDate;
  const canProceedStep2 = selectedCategories.length > 0 && selectedCategories.every((c) => c.budgetedAmount > 0);

  const totalBudget = selectedCategories.reduce((sum, c) => sum + c.budgetedAmount, 0);

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
      <Navigation />

      <main className="container mx-auto px-4 py-4 sm:py-6 lg:py-8">
        {/* Header */}
        <div className="mb-6 lg:mb-8">
          {/* Navigation Bar */}
          <div className="flex items-center justify-between mb-6">
            <Link href="/budgets">
              <Button variant="secondary" size="sm" className="flex items-center gap-2">
                <ArrowLeftIcon className="w-4 h-4" />
                <span className="hidden sm:inline">{t('backToBudgets')}</span>
                <span className="sm:hidden">{tCommon('back')}</span>
              </Button>
            </Link>
          </div>

          {/* Page Title */}
          <div className="text-center mb-8">
            <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900 mb-2">
              {t('wizard.title')}
            </h1>
            <p className="text-gray-600 text-sm sm:text-base">
              {t('wizard.subtitle')}
            </p>
          </div>

          {/* Progress Steps */}
          <div className="flex items-center justify-center">
            {[1, 2, 3].map((s) => (
              <div key={s} className="flex items-center">
                <div
                  className={cn(
                    'w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium border-2 transition-colors',
                    step > s
                      ? 'bg-primary-600 border-primary-600 text-white'
                      : step === s
                      ? 'bg-primary-600 border-primary-600 text-white'
                      : 'bg-white border-gray-300 text-gray-600'
                  )}
                >
                  {step > s ? <Check className="h-4 w-4" /> : s}
                </div>
                {s < 3 && (
                  <div
                    className={cn(
                      'w-16 h-1 mx-2 transition-colors',
                      step > s ? 'bg-primary-600' : 'bg-gray-300'
                    )}
                  />
                )}
              </div>
            ))}
          </div>
        </div>

        {/* Form Card */}
        <div className="max-w-2xl mx-auto">
          {/* Step 1: Basic Information */}
          {step === 1 && (
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardHeader>
                <CardTitle>{t('wizard.step1Title')}</CardTitle>
                <p className="text-sm text-muted-foreground">{t('wizard.step1Description')}</p>
              </CardHeader>
              <CardContent className="space-y-6">
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

                <div className="space-y-2">
                  <Label htmlFor="periodType">{t('wizard.periodType')}</Label>
                  <Select
                    id="periodType"
                    value={periodType}
                    onChange={(e) => setPeriodType(e.target.value as BudgetPeriodType)}
                    className="w-full"
                  >
                    <option value="Monthly">{t('wizard.monthly')}</option>
                    <option value="Weekly">{t('wizard.weekly')}</option>
                    <option value="Biweekly">{t('wizard.biweekly')}</option>
                    <option value="Custom">{t('wizard.custom')}</option>
                  </Select>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="startDate">{t('wizard.startDate')} *</Label>
                    <Input
                      id="startDate"
                      type="date"
                      value={startDate}
                      onChange={(e) => setStartDate(e.target.value)}
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="endDate">{t('wizard.endDate')} *</Label>
                    <Input
                      id="endDate"
                      type="date"
                      value={endDate}
                      onChange={(e) => setEndDate(e.target.value)}
                      disabled={periodType !== 'Custom'}
                    />
                  </div>
                </div>

                <div className="flex items-start space-x-3">
                  <Checkbox
                    id="isRecurring"
                    checked={isRecurring}
                    onCheckedChange={(checked) => setIsRecurring(checked === true)}
                  />
                  <div className="space-y-1">
                    <Label htmlFor="isRecurring" className="cursor-pointer">
                      {t('wizard.isRecurring')}
                    </Label>
                    <p className="text-sm text-muted-foreground">
                      {t('wizard.isRecurringHelp')}
                    </p>
                  </div>
                </div>

                <div className="flex justify-end">
                  <Button onClick={() => setStep(2)} disabled={!canProceedStep1}>
                    {tCommon('next')}
                    <ArrowRight className="h-4 w-4 ml-2" />
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Step 2: Add Categories */}
          {step === 2 && (
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardHeader>
                <CardTitle>{t('wizard.step2Title')}</CardTitle>
                <p className="text-sm text-muted-foreground">{t('wizard.step2Description')}</p>
              </CardHeader>
              <CardContent className="space-y-6">
                {/* Category Search */}
                <div className="space-y-2">
                  <Label>{t('wizard.selectCategories')}</Label>
                  <div className="relative">
                    <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                    <Input
                      value={searchTerm}
                      onChange={(e) => setSearchTerm(e.target.value)}
                      placeholder={t('wizard.searchCategories')}
                      className="pl-9"
                    />
                  </div>
                  {isLoadingCategories ? (
                    <div className="space-y-2">
                      <Skeleton className="h-10 w-full" />
                      <Skeleton className="h-10 w-full" />
                    </div>
                  ) : filteredCategories.length === 0 ? (
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
                </div>

                {/* Selected Categories */}
                <div className="space-y-2">
                  <Label>
                    {t('wizard.selectedCategories', { count: selectedCategories.length })}
                  </Label>
                  {selectedCategories.length === 0 ? (
                    <p className="text-sm text-muted-foreground py-4 text-center border rounded-md">
                      {t('wizard.noCategoriesSelected')}
                    </p>
                  ) : (
                    <div className="space-y-2">
                      {selectedCategories.map((selection) => (
                        <div key={selection.categoryId} className="border rounded-lg p-3 space-y-2">
                          <div className="flex items-center justify-between">
                            <div className="flex items-center gap-2">
                              {renderCategoryIcon(selection.categoryIcon, 'h-4 w-4')}
                              <span className="font-medium">{selection.categoryName}</span>
                            </div>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => handleRemoveCategory(selection.categoryId)}
                              className="text-destructive hover:text-destructive"
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </div>

                          <div className="space-y-1.5">
                            <Label className="text-sm">{t('wizard.budgetAmount')}</Label>
                            <div className="flex items-center gap-2">
                              <Input
                                type="number"
                                min="0"
                                step="10"
                                value={selection.budgetedAmount || ''}
                                onChange={(e) =>
                                  handleUpdateCategoryAmount(selection.categoryId, parseFloat(e.target.value) || 0)
                                }
                                placeholder="0.00"
                              />
                              {selection.suggestion && (
                                <Button
                                  variant="outline"
                                  size="sm"
                                  onClick={() => handleUseSuggestion(selection.categoryId)}
                                  className="whitespace-nowrap"
                                >
                                  <Lightbulb className="h-4 w-4 mr-1" />
                                  {t('wizard.useSuggestion')}
                                </Button>
                              )}
                            </div>
                            {selection.suggestion && (
                              <p className="text-xs text-muted-foreground">
                                {t('wizard.suggestedAmount', {
                                  amount: formatCurrency(selection.suggestion.suggestedBudget),
                                })}{' '}
                                ({t('wizard.basedOnHistory', { months: selection.suggestion.monthsAnalyzed })})
                              </p>
                            )}
                          </div>

                          <div className="flex items-center gap-4 flex-wrap">
                            <div className="flex items-center space-x-2">
                              <Checkbox
                                id={`rollover-${selection.categoryId}`}
                                checked={selection.allowRollover}
                                onCheckedChange={(checked) =>
                                  handleToggleRollover(selection.categoryId, checked === true)
                                }
                              />
                              <Label htmlFor={`rollover-${selection.categoryId}`} className="text-sm">
                                {t('wizard.allowRollover')}
                              </Label>
                            </div>
                            <div className="flex items-center space-x-2">
                              <Checkbox
                                id={`subcategories-${selection.categoryId}`}
                                checked={selection.includeSubcategories}
                                onCheckedChange={(checked) =>
                                  handleToggleSubcategories(selection.categoryId, checked === true)
                                }
                              />
                              <Label htmlFor={`subcategories-${selection.categoryId}`} className="text-sm">
                                {t('wizard.includeSubcategories')}
                              </Label>
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                {/* Total */}
                {selectedCategories.length > 0 && (
                  <div className="flex justify-between items-center pt-4 border-t">
                    <span className="font-medium">{t('totalBudgeted')}</span>
                    <span className="text-xl font-bold">{formatCurrency(totalBudget)}</span>
                  </div>
                )}

                <div className="flex justify-between">
                  <Button variant="secondary" onClick={() => setStep(1)}>
                    <ArrowLeftIcon className="w-4 h-4 mr-2" />
                    {tCommon('back')}
                  </Button>
                  <Button onClick={() => setStep(3)} disabled={!canProceedStep2}>
                    {tCommon('next')}
                    <ArrowRight className="h-4 w-4 ml-2" />
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Step 3: Review & Create */}
          {step === 3 && (
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardHeader>
                <CardTitle>{t('wizard.step3Title')}</CardTitle>
                <p className="text-sm text-muted-foreground">{t('wizard.step3Description')}</p>
              </CardHeader>
              <CardContent className="space-y-6">
                <div className="space-y-4">
                  <h3 className="font-medium">{t('wizard.reviewBudget')}</h3>

                  <div className="grid grid-cols-2 gap-4 text-sm">
                    <div>
                      <span className="text-muted-foreground">{t('wizard.budgetName')}</span>
                      <p className="font-medium">{name}</p>
                    </div>
                    <div>
                      <span className="text-muted-foreground">{t('wizard.periodType')}</span>
                      <p className="font-medium">{t(`wizard.${periodType.toLowerCase()}`)}</p>
                    </div>
                    <div>
                      <span className="text-muted-foreground">{t('wizard.budgetPeriod')}</span>
                      <p className="font-medium">
                        {new Date(startDate).toLocaleDateString()} - {new Date(endDate).toLocaleDateString()}
                      </p>
                    </div>
                    <div>
                      <span className="text-muted-foreground">{t('wizard.isRecurring')}</span>
                      <p className="font-medium">{isRecurring ? tCommon('yes') : tCommon('no')}</p>
                    </div>
                  </div>

                  {description && (
                    <div className="text-sm">
                      <span className="text-muted-foreground">{tCommon('description')}</span>
                      <p>{description}</p>
                    </div>
                  )}
                </div>

                <div className="space-y-3">
                  <h3 className="font-medium">{t('wizard.categoriesAndAmounts')}</h3>
                  <div className="border rounded-lg divide-y">
                    {selectedCategories.map((selection) => (
                      <div
                        key={selection.categoryId}
                        className="flex items-center justify-between px-4 py-3"
                      >
                        <div className="flex items-center gap-2">
                          {renderCategoryIcon(selection.categoryIcon, 'h-4 w-4')}
                          <span>{selection.categoryName}</span>
                          {selection.allowRollover && (
                            <Badge variant="outline" className="text-xs">Rollover</Badge>
                          )}
                          {selection.includeSubcategories && (
                            <Badge variant="outline" className="text-xs">+Subs</Badge>
                          )}
                        </div>
                        <span className="font-medium">{formatCurrency(selection.budgetedAmount)}</span>
                      </div>
                    ))}
                    <div className="flex items-center justify-between px-4 py-3 bg-muted/50">
                      <span className="font-medium">{tCommon('total')}</span>
                      <span className="text-lg font-bold">{formatCurrency(totalBudget)}</span>
                    </div>
                  </div>
                </div>

                <div className="flex justify-between">
                  <Button variant="secondary" onClick={() => setStep(2)}>
                    <ArrowLeftIcon className="w-4 h-4 mr-2" />
                    {tCommon('back')}
                  </Button>
                  <Button onClick={handleSubmit} disabled={isSubmitting}>
                    {isSubmitting ? t('wizard.creating') : t('wizard.createBudget')}
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}
        </div>
      </main>
    </div>
  );
}
