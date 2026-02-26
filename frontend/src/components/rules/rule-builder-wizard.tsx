'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import { CategoryPicker } from '@/components/forms/category-picker';
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  CheckCircleIcon,
  PlayIcon,
  SparklesIcon,
  PlusIcon,
  XMarkIcon
} from '@heroicons/react/24/outline';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';

interface Category {
  id: number;
  name: string;
  type: number;
  parentId: number | null;
  color?: string;
  icon?: string;
  isSystem?: boolean;
  children?: Category[];
}

interface RuleCondition {
  field: string;
  operator: string;
  value: string;
  isCaseSensitive: boolean;
  order: number;
}

interface TestTransaction {
  id: number;
  description: string;
  amount: number;
  transactionDate: string;
  accountName: string;
  categoryName?: string;
}

const RULE_TYPES = [
  { value: 'Contains', labelKey: 'contains', descKey: 'containsDesc', enumValue: 1 },
  { value: 'StartsWith', labelKey: 'startsWith', descKey: 'startsWithDesc', enumValue: 2 },
  { value: 'EndsWith', labelKey: 'endsWith', descKey: 'endsWithDesc', enumValue: 3 },
  { value: 'Equals', labelKey: 'equals', descKey: 'equalsDesc', enumValue: 4 },
  { value: 'Regex', labelKey: 'regex', descKey: 'regexDesc', enumValue: 5 }
];

const CONDITION_FIELDS = [
  { value: 'Description', labelKey: 'description' },
  { value: 'UserDescription', labelKey: 'userDescription' },
  { value: 'Amount', labelKey: 'amount' },
  { value: 'AccountType', labelKey: 'accountType' },
  { value: 'AccountName', labelKey: 'accountName' },
  { value: 'TransactionType', labelKey: 'transactionType' },
  { value: 'ReferenceNumber', labelKey: 'referenceNumber' },
  { value: 'Notes', labelKey: 'notes' }
];

const CONDITION_OPERATORS = [
  { value: 'Equals', labelKey: 'equals' },
  { value: 'NotEquals', labelKey: 'notEquals' },
  { value: 'Contains', labelKey: 'contains' },
  { value: 'NotContains', labelKey: 'notContains' },
  { value: 'StartsWith', labelKey: 'startsWith' },
  { value: 'EndsWith', labelKey: 'endsWith' },
  { value: 'GreaterThan', labelKey: 'greaterThan' },
  { value: 'LessThan', labelKey: 'lessThan' },
  { value: 'GreaterThanOrEqual', labelKey: 'greaterThanOrEqual' },
  { value: 'LessThanOrEqual', labelKey: 'lessThanOrEqual' },
  { value: 'Regex', labelKey: 'regex' }
];

const ACCOUNT_TYPES = [
  { value: 'Checking', labelKey: 'checking' },
  { value: 'Savings', labelKey: 'savings' },
  { value: 'CreditCard', labelKey: 'creditCard' },
  { value: 'Investment', labelKey: 'investment' },
  { value: 'Loan', labelKey: 'loan' },
  { value: 'Other', labelKey: 'other' }
];

// Helper function to get category type color class

export function RuleBuilderWizard() {
  const router = useRouter();
  const t = useTranslations('rules');

  const [currentStep, setCurrentStep] = useState(1);
  const [categories, setCategories] = useState<Category[]>([]);
  const [testResults, setTestResults] = useState<TestTransaction[]>([]);
  const [loading, setLoading] = useState(false);
  const [testLoading, setTestLoading] = useState(false);

  // Form data
  const [formData, setFormData] = useState({
    name: '',
    description: '',
    type: 'Contains',
    pattern: '',
    isCaseSensitive: false,
    priority: 0,
    isActive: true,
    categoryId: '',
    minAmount: '',
    maxAmount: '',
    accountTypes: [] as string[],
    logic: 'All',
    conditions: [] as RuleCondition[]
  });


  const steps = [
    { number: 1, title: t('builder.steps.basicInfo'), description: t('builder.steps.basicInfoDesc') },
    { number: 2, title: t('builder.steps.pattern'), description: t('builder.steps.patternDesc') },
    { number: 3, title: t('builder.steps.category'), description: t('builder.steps.categoryDesc') },
    { number: 4, title: t('builder.steps.advanced'), description: t('builder.steps.advancedDesc') },
    { number: 5, title: t('builder.steps.test'), description: t('builder.steps.testDesc') },
    { number: 6, title: t('builder.steps.review'), description: t('builder.steps.reviewDesc') }
  ];

  useEffect(() => {
    loadCategories();
  }, []);

  const loadCategories = async () => {
    try {
      const response = await apiClient.get('/api/categories');
      setCategories(response as any);
    } catch (error) {
      console.error('Failed to load categories:', error);
      toast.error(t('toasts.loadCategoriesFailed'));
    }
  };

  const updateFormData = (field: string, value: any) => {
    setFormData(prev => ({
      ...prev,
      [field]: value
    }));
  };

  const addCondition = () => {
    const newCondition: RuleCondition = {
      field: 'Description',
      operator: 'Contains',
      value: '',
      isCaseSensitive: false,
      order: formData.conditions.length
    };
    updateFormData('conditions', [...formData.conditions, newCondition]);
  };

  const updateCondition = (index: number, field: string, value: any) => {
    const updatedConditions = [...formData.conditions];
    updatedConditions[index] = {
      ...updatedConditions[index],
      [field]: value
    };
    updateFormData('conditions', updatedConditions);
  };

  const removeCondition = (index: number) => {
    const updatedConditions = formData.conditions.filter((_, i) => i !== index);
    updateFormData('conditions', updatedConditions);
  };

  const testRule = async () => {
    try {
      setTestLoading(true);

      // Convert string values to enum values for backend (same as createRule)
      const ruleType = RULE_TYPES.find(rt => rt.value === formData.type);
      const logicValue = formData.logic === 'All' ? 1 : 2; // All = 1, Any = 2

      // Create a temporary rule object for testing
      const tempRule = {
        name: formData.name,
        description: formData.description,
        type: ruleType?.enumValue || 1, // Default to Contains if not found
        pattern: formData.pattern,
        isCaseSensitive: formData.isCaseSensitive,
        priority: formData.priority,
        isActive: true,
        categoryId: parseInt(formData.categoryId),
        minAmount: formData.minAmount ? parseFloat(formData.minAmount) : null,
        maxAmount: formData.maxAmount ? parseFloat(formData.maxAmount) : null,
        accountTypes: formData.accountTypes.length > 0 ? formData.accountTypes.join(',') : null,
        logic: logicValue,
        conditions: formData.conditions.map(condition => ({
          field: condition.field, // TODO: Convert to enum if needed
          operator: condition.operator, // TODO: Convert to enum if needed
          value: condition.value,
          isCaseSensitive: condition.isCaseSensitive,
          order: condition.order
        }))
      };

      // For now, we'll create a temporary rule and test it
      // In production, you might want a dedicated test endpoint
      const createResponse = await apiClient.post('/api/rules', tempRule) as any;
      const ruleId = createResponse.id; // Fixed: removed .data accessor

      // Test the rule
      const testResponse = await apiClient.post(`/api/rules/${ruleId}/test?maxResults=20`, null) as any;

      setTestResults(testResponse.matchingTransactions || []);

      // Delete the temporary rule
      await apiClient.delete(`/api/rules/${ruleId}`);

    } catch (error) {
      console.error('Failed to test rule:', error);
      toast.error(t('toasts.testFailed'));
    } finally {
      setTestLoading(false);
    }
  };

  const createRule = async () => {
    try {
      setLoading(true);

      // Convert string values to enum values for backend
      const ruleType = RULE_TYPES.find(rt => rt.value === formData.type);
      const logicValue = formData.logic === 'All' ? 1 : 2; // All = 1, Any = 2

      const ruleData = {
        name: formData.name,
        description: formData.description,
        type: ruleType?.enumValue || 1, // Default to Contains if not found
        pattern: formData.pattern,
        isCaseSensitive: formData.isCaseSensitive,
        priority: formData.priority,
        isActive: formData.isActive,
        categoryId: parseInt(formData.categoryId),
        minAmount: formData.minAmount ? parseFloat(formData.minAmount) : null,
        maxAmount: formData.maxAmount ? parseFloat(formData.maxAmount) : null,
        accountTypes: formData.accountTypes.length > 0 ? formData.accountTypes.join(',') : null,
        logic: logicValue,
        conditions: formData.conditions.map(condition => ({
          field: condition.field, // TODO: Convert to enum if needed
          operator: condition.operator, // TODO: Convert to enum if needed
          value: condition.value,
          isCaseSensitive: condition.isCaseSensitive,
          order: condition.order
        }))
      };

      await apiClient.post('/api/rules', ruleData);
      toast.success(t('toasts.createSuccess'));
      router.push('/rules');

    } catch (error) {
      console.error('Failed to create rule:', error);
      toast.error(t('toasts.createFailed'));
    } finally {
      setLoading(false);
    }
  };

  const nextStep = () => {
    if (currentStep < steps.length) {
      setCurrentStep(currentStep + 1);
    }
  };

  const prevStep = () => {
    if (currentStep > 1) {
      setCurrentStep(currentStep - 1);
    }
  };

  const canProceed = () => {
    switch (currentStep) {
      case 1: return formData.name.trim().length > 0;
      case 2: return formData.pattern.trim().length > 0;
      case 3: return formData.categoryId.length > 0;
      case 4: case 5: return true;
      case 6: return true;
      default: return false;
    }
  };

  const selectedCategory = categories.find(c => c.id.toString() === formData.categoryId);

  return (
    <div className="space-y-5">
      {/* Progress Steps */}
      <div className="flex items-center justify-between">
        {steps.map((step, index) => (
          <div key={step.number} className="flex items-center">
            <div className={cn(
              'flex items-center justify-center w-10 h-10 rounded-full border-2 text-sm font-semibold transition-colors',
              currentStep > step.number && 'bg-emerald-500 border-emerald-500 text-white',
              currentStep === step.number && 'bg-violet-500 border-violet-500 text-white',
              currentStep < step.number && 'bg-white border-slate-300 text-slate-400'
            )}>
              {currentStep > step.number ? (
                <CheckCircleIcon className="w-6 h-6" />
              ) : (
                step.number
              )}
            </div>
            {index < steps.length - 1 && (
              <div className={cn(
                'w-16 h-1 mx-2 rounded-full transition-colors',
                currentStep > step.number ? 'bg-emerald-500' : 'bg-slate-200'
              )} />
            )}
          </div>
        ))}
      </div>

      <section className="rounded-[26px] border border-violet-100/60 bg-white/90 shadow-lg shadow-violet-200/20 backdrop-blur-xs overflow-hidden">
        {/* Card Header */}
        <div className="p-6 pb-0">
          <h2 className="font-[var(--font-dash-sans)] text-lg font-semibold text-slate-900">
            {steps[currentStep - 1].title}
          </h2>
          <p className="text-sm text-slate-500 mt-1">{steps[currentStep - 1].description}</p>
        </div>

        <div className="p-6">
          {/* Step 1: Basic Info */}
          {currentStep === 1 && (
            <div className="space-y-4">
              <div>
                <Label htmlFor="name">{t('builder.form.ruleName')}</Label>
                <Input
                  id="name"
                  value={formData.name}
                  onChange={(e) => updateFormData('name', e.target.value)}
                  placeholder={t('builder.form.ruleNamePlaceholder')}
                />
              </div>
              <div>
                <Label htmlFor="description">{t('builder.form.description')}</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => updateFormData('description', e.target.value)}
                  placeholder={t('builder.form.descriptionPlaceholder')}
                  rows={3}
                />
              </div>
              <div>
                <Label htmlFor="priority">{t('builder.form.priority')}</Label>
                <Input
                  id="priority"
                  type="number"
                  value={formData.priority}
                  onChange={(e) => updateFormData('priority', parseInt(e.target.value) || 0)}
                  min="0"
                />
                <p className="text-sm text-slate-500 mt-1">{t('builder.form.priorityHelp')}</p>
              </div>
            </div>
          )}

          {/* Step 2: Pattern */}
          {currentStep === 2 && (
            <div className="space-y-4">
              <div>
                <Label htmlFor="type">{t('builder.form.ruleType')}</Label>
                <select
                  id="type"
                  value={formData.type}
                  onChange={(e) => updateFormData('type', e.target.value)}
                  className="select"
                >
                  {RULE_TYPES.map((type) => (
                    <option key={type.value} value={type.value}>
                      {t(`builder.ruleTypes.${type.labelKey}`)}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <Label htmlFor="pattern">{t('builder.form.pattern')}</Label>
                <Input
                  id="pattern"
                  value={formData.pattern}
                  onChange={(e) => updateFormData('pattern', e.target.value)}
                  placeholder={t('builder.form.patternPlaceholder')}
                />
                <p className="text-sm text-slate-500 mt-1">
                  {t('builder.form.patternHelp')}
                </p>
              </div>
              <label className="flex items-center gap-2">
                <input
                  type="checkbox"
                  id="caseSensitive"
                  checked={formData.isCaseSensitive}
                  onChange={(e) => updateFormData('isCaseSensitive', e.target.checked)}
                  className="rounded border-slate-300 text-violet-600 focus:ring-violet-500"
                />
                <Label htmlFor="caseSensitive" className="cursor-pointer">{t('builder.form.caseSensitive')}</Label>
              </label>
            </div>
          )}

          {/* Step 3: Category */}
          {currentStep === 3 && (
            <div className="space-y-4">
              <div>
                <Label htmlFor="category">{t('builder.form.targetCategory')}</Label>
                <CategoryPicker
                  value={formData.categoryId}
                  onChange={(categoryId) => updateFormData('categoryId', categoryId.toString())}
                  categories={categories}
                  placeholder={t('builder.form.selectTargetCategory')}
                  disableQuickPicks={false}
                />
                <p className="text-sm text-slate-500 mt-1">
                  {t('builder.form.targetCategoryHelp')}
                </p>
              </div>
              {selectedCategory && (
                <div className="p-4 bg-violet-50 rounded-2xl">
                  <h4 className="font-medium text-violet-900">{t('builder.form.selectedCategory')}</h4>
                  <p className="text-violet-700">{selectedCategory.name}</p>
                </div>
              )}
            </div>
          )}

          {/* Step 4: Advanced */}
          {currentStep === 4 && (
            <div className="space-y-6">
              {/* Amount Range */}
              <div>
                <h4 className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-3">
                  {t('builder.advanced.amountRange')}
                </h4>
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <Label htmlFor="minAmount">{t('builder.advanced.minAmount')}</Label>
                    <Input
                      id="minAmount"
                      type="number"
                      step="0.01"
                      value={formData.minAmount}
                      onChange={(e) => updateFormData('minAmount', e.target.value)}
                      placeholder="0.00"
                    />
                  </div>
                  <div>
                    <Label htmlFor="maxAmount">{t('builder.advanced.maxAmount')}</Label>
                    <Input
                      id="maxAmount"
                      type="number"
                      step="0.01"
                      value={formData.maxAmount}
                      onChange={(e) => updateFormData('maxAmount', e.target.value)}
                      placeholder="1000.00"
                    />
                  </div>
                </div>
              </div>

              {/* Account Types */}
              <div>
                <h4 className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-3">
                  {t('builder.advanced.accountTypes')}
                </h4>
                <div className="grid grid-cols-2 gap-2">
                  {ACCOUNT_TYPES.map((type) => (
                    <label key={type.value} className="flex items-center gap-2">
                      <input
                        type="checkbox"
                        checked={formData.accountTypes.includes(type.value)}
                        onChange={(e) => {
                          if (e.target.checked) {
                            updateFormData('accountTypes', [...formData.accountTypes, type.value]);
                          } else {
                            updateFormData('accountTypes', formData.accountTypes.filter(t => t !== type.value));
                          }
                        }}
                        className="rounded border-slate-300 text-violet-600 focus:ring-violet-500"
                      />
                      <span className="text-sm text-slate-700">{t(`builder.accountTypes.${type.labelKey}`)}</span>
                    </label>
                  ))}
                </div>
                <p className="text-sm text-slate-500 mt-2">
                  {t('builder.advanced.accountTypesHelp')}
                </p>
              </div>

              {/* Advanced Conditions */}
              <div>
                <div className="flex items-center justify-between mb-3">
                  <h4 className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                    {t('builder.advanced.advancedConditions')}
                  </h4>
                  <Button variant="secondary" size="sm" onClick={addCondition} className="flex items-center gap-1.5">
                    <PlusIcon className="w-4 h-4" />
                    {t('builder.advanced.addCondition')}
                  </Button>
                </div>

                {formData.conditions.length > 0 && (
                  <div className="space-y-4">
                    <div>
                      <Label htmlFor="logic">{t('builder.advanced.logic')}</Label>
                      <select
                        id="logic"
                        value={formData.logic}
                        onChange={(e) => updateFormData('logic', e.target.value)}
                        className="select w-32"
                      >
                        <option value="All">{t('builder.advanced.logicAll')}</option>
                        <option value="Any">{t('builder.advanced.logicAny')}</option>
                      </select>
                    </div>

                    {formData.conditions.map((condition, index) => (
                      <div key={index} className="p-4 border border-slate-200 rounded-2xl space-y-3">
                        <div className="flex items-center justify-between">
                          <h5 className="text-sm font-medium text-slate-700">
                            {t('builder.advanced.condition', { number: index + 1 })}
                          </h5>
                          <Button
                            variant="secondary"
                            size="sm"
                            onClick={() => removeCondition(index)}
                            className="w-8 h-8 p-0 text-slate-400 hover:text-red-600"
                          >
                            <XMarkIcon className="w-4 h-4" />
                          </Button>
                        </div>

                        <div className="grid grid-cols-3 gap-3">
                          <div>
                            <Label htmlFor={`field-${index}`}>{t('builder.advanced.field')}</Label>
                            <select
                              id={`field-${index}`}
                              value={condition.field}
                              onChange={(e) => updateCondition(index, 'field', e.target.value)}
                              className="select"
                            >
                              {CONDITION_FIELDS.map((field) => (
                                <option key={field.value} value={field.value}>
                                  {t(`builder.conditionFields.${field.labelKey}`)}
                                </option>
                              ))}
                            </select>
                          </div>

                          <div>
                            <Label htmlFor={`operator-${index}`}>{t('builder.advanced.operator')}</Label>
                            <select
                              id={`operator-${index}`}
                              value={condition.operator}
                              onChange={(e) => updateCondition(index, 'operator', e.target.value)}
                              className="select"
                            >
                              {CONDITION_OPERATORS.map((op) => (
                                <option key={op.value} value={op.value}>
                                  {t(`builder.conditionOperators.${op.labelKey}`)}
                                </option>
                              ))}
                            </select>
                          </div>

                          <div>
                            <Label>{t('builder.advanced.value')}</Label>
                            <Input
                              value={condition.value}
                              onChange={(e) => updateCondition(index, 'value', e.target.value)}
                              placeholder={t('builder.advanced.valuePlaceholder')}
                            />
                          </div>
                        </div>

                        <label className="flex items-center gap-2">
                          <input
                            type="checkbox"
                            checked={condition.isCaseSensitive}
                            onChange={(e) => updateCondition(index, 'isCaseSensitive', e.target.checked)}
                            className="rounded border-slate-300 text-violet-600 focus:ring-violet-500"
                          />
                          <span className="text-sm text-slate-600">{t('builder.advanced.caseSensitiveCondition')}</span>
                        </label>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Step 5: Test */}
          {currentStep === 5 && (
            <div className="space-y-4">
              <div className="text-center">
                <Button onClick={testRule} disabled={testLoading} className="mb-4">
                  {testLoading ? (
                    <>
                      <div className="animate-spin w-4 h-4 mr-2 border-2 border-white border-t-transparent rounded-full" />
                      {t('builder.test.testing')}
                    </>
                  ) : (
                    <>
                      <PlayIcon className="w-4 h-4 mr-2" />
                      {t('builder.test.testRule')}
                    </>
                  )}
                </Button>
                <p className="text-sm text-slate-500">
                  {t('builder.test.testHelp')}
                </p>
              </div>

              {testResults.length > 0 && (
                <div>
                  <h4 className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-3">
                    {t('builder.test.results', { count: testResults.length })}
                  </h4>
                  <div className="space-y-2 max-h-96 overflow-y-auto">
                    {testResults.map((transaction) => (
                      <div key={transaction.id} className="p-3 bg-slate-50 rounded-xl">
                        <div className="flex justify-between items-start gap-4">
                          <div className="min-w-0 flex-1">
                            <p className="font-medium text-slate-900 truncate">{transaction.description}</p>
                            <p className="text-sm text-slate-500 mt-0.5">
                              {transaction.accountName} &middot; {new Date(transaction.transactionDate).toLocaleDateString()}
                            </p>
                          </div>
                          <div className="text-right shrink-0">
                            <p className={cn(
                              'font-[var(--font-dash-mono)] font-medium',
                              transaction.amount >= 0 ? 'text-emerald-600' : 'text-red-600'
                            )}>
                              ${Math.abs(transaction.amount).toFixed(2)}
                            </p>
                            {transaction.categoryName && (
                              <p className="text-xs text-slate-400">{transaction.categoryName}</p>
                            )}
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {testResults.length === 0 && testLoading === false && (
                <div className="text-center py-8 text-slate-400">
                  <p>{t('builder.test.noResults')}</p>
                </div>
              )}
            </div>
          )}

          {/* Step 6: Review */}
          {currentStep === 6 && (
            <div className="space-y-6">
              <div className="bg-slate-50 p-6 rounded-2xl">
                <h4 className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-4">
                  {t('builder.review.summary')}
                </h4>
                <div className="space-y-3">
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <span className="text-xs text-slate-500">{t('builder.review.name')}</span>
                      <p className="font-medium text-slate-900">{formData.name}</p>
                    </div>
                    <div>
                      <span className="text-xs text-slate-500">{t('builder.review.type')}</span>
                      <p className="font-medium text-slate-900">{formData.type}</p>
                    </div>
                    <div>
                      <span className="text-xs text-slate-500">{t('builder.review.pattern')}</span>
                      <code className="bg-white px-2 py-0.5 rounded text-sm text-slate-700">{formData.pattern}</code>
                    </div>
                    <div>
                      <span className="text-xs text-slate-500">{t('builder.review.category')}</span>
                      <p className="font-medium text-slate-900">{selectedCategory?.name}</p>
                    </div>
                    <div>
                      <span className="text-xs text-slate-500">{t('builder.review.priority')}</span>
                      <p className="font-[var(--font-dash-mono)] font-medium text-slate-900">{formData.priority}</p>
                    </div>
                    <div>
                      <span className="text-xs text-slate-500">{t('builder.review.caseSensitive')}</span>
                      <p className="font-medium text-slate-900">{formData.isCaseSensitive ? t('builder.review.yes') : t('builder.review.no')}</p>
                    </div>
                  </div>

                  {(formData.minAmount || formData.maxAmount) && (
                    <div>
                      <span className="text-xs text-slate-500">{t('builder.review.amountRange')}</span>
                      <p className="font-[var(--font-dash-mono)] font-medium text-slate-900">
                        {formData.minAmount ? `$${formData.minAmount}` : t('builder.review.noMinimum')} - {formData.maxAmount ? `$${formData.maxAmount}` : t('builder.review.noMaximum')}
                      </p>
                    </div>
                  )}

                  {formData.accountTypes.length > 0 && (
                    <div>
                      <span className="text-xs text-slate-500">{t('builder.review.accountTypes')}</span>
                      <div className="flex flex-wrap gap-2 mt-1">
                        {formData.accountTypes.map((type) => {
                          const accountType = ACCOUNT_TYPES.find(at => at.value === type);
                          return (
                            <Badge key={type} variant="secondary" className="bg-slate-100 text-slate-700">
                              {accountType ? t(`builder.accountTypes.${accountType.labelKey}`) : type}
                            </Badge>
                          );
                        })}
                      </div>
                    </div>
                  )}

                  {formData.conditions.length > 0 && (
                    <div>
                      <span className="text-xs text-slate-500">{t('builder.review.advancedConditions', { logic: formData.logic })}</span>
                      <div className="space-y-2 mt-2">
                        {formData.conditions.map((condition, index) => (
                          <div key={index} className="text-sm bg-white p-2 rounded-lg text-slate-700">
                            {condition.field} {condition.operator} &quot;{condition.value}&quot;
                            {condition.isCaseSensitive && ` ${t('builder.review.caseSensitiveNote')}`}
                          </div>
                        ))}
                      </div>
                    </div>
                  )}

                  {formData.description && (
                    <div>
                      <span className="text-xs text-slate-500">{t('builder.review.description')}</span>
                      <p className="text-sm text-slate-700">{formData.description}</p>
                    </div>
                  )}
                </div>
              </div>

              <label className="flex items-center gap-2">
                <input
                  type="checkbox"
                  id="isActive"
                  checked={formData.isActive}
                  onChange={(e) => updateFormData('isActive', e.target.checked)}
                  className="rounded border-slate-300 text-violet-600 focus:ring-violet-500"
                />
                <Label htmlFor="isActive" className="cursor-pointer">{t('builder.review.activateImmediately')}</Label>
              </label>
            </div>
          )}
        </div>
      </section>

      {/* Navigation Buttons */}
      <div className="flex justify-between">
        <Button
          variant="secondary"
          onClick={prevStep}
          disabled={currentStep === 1}
        >
          <ChevronLeftIcon className="w-4 h-4 mr-2" />
          {t('builder.navigation.previous')}
        </Button>

        <div className="flex gap-3">
          {currentStep < steps.length ? (
            <Button onClick={nextStep} disabled={!canProceed()}>
              {t('builder.navigation.next')}
              <ChevronRightIcon className="w-4 h-4 ml-2" />
            </Button>
          ) : (
            <Button onClick={createRule} disabled={loading || !canProceed()}>
              {loading ? (
                <>
                  <div className="animate-spin w-4 h-4 mr-2 border-2 border-white border-t-transparent rounded-full" />
                  {t('builder.navigation.creating')}
                </>
              ) : (
                <>
                  <SparklesIcon className="w-4 h-4 mr-2" />
                  {t('builder.navigation.createRule')}
                </>
              )}
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}
