'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { useEffect, useState, useMemo } from 'react';
import dynamic from 'next/dynamic';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { formatCurrency, cn } from '@/lib/utils';
import { apiClient } from '@/lib/api-client';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import {
  PlusIcon,
  TagIcon,
  MagnifyingGlassIcon,
  EllipsisVerticalIcon,
  PencilIcon,
  TrashIcon,
  FunnelIcon,
  SparklesIcon,
  EyeIcon,
  ArrowPathIcon,
  ChevronDownIcon,
} from '@heroicons/react/24/outline';

// Dynamic import for BankCategoryMappings to avoid SSR issues
const BankCategoryMappings = dynamic(
  () => import('@/components/bank-category-mappings'),
  {
    ssr: false,
    loading: () => (
      <div className="space-y-4">
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="animate-pulse">
            <div className="h-24 bg-slate-100 rounded-lg"></div>
          </div>
        ))}
      </div>
    )
  }
);
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
  DropdownMenuSeparator,
} from "@/components/ui/dropdown-menu";
import { toast } from 'sonner';

interface Category {
  id: number;
  name: string;
  description?: string;
  color?: string;
  icon?: string;
  type: number;
  isSystemCategory: boolean;
  isActive: boolean;
  sortOrder: number;
  parentCategoryId?: number;
  parentCategoryName?: string;
  fullPath: string;
  transactionCount: number;
  totalAmount: number;
  createdAt: string;
  updatedAt: string;
}

const categoryTypeBadgeColors: Record<number, string> = {
  1: 'bg-emerald-100 text-emerald-700',
  2: 'bg-red-100 text-red-700',
  3: 'bg-blue-100 text-blue-700',
};

export default function CategoriesPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const t = useTranslations('categories');
  const tCommon = useTranslations('common');
  const [categories, setCategories] = useState<Category[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [showFilters, setShowFilters] = useState(false);
  const [selectedType, setSelectedType] = useState<number | ''>('');
  const [initializingCategories, setInitializingCategories] = useState(false);
  const [activeTab, setActiveTab] = useState<'categories' | 'bank-mappings'>('categories');
  const [collapsedGroups, setCollapsedGroups] = useState<Set<number>>(new Set());

  const getCategoryTypeLabel = (type: number) => {
    switch (type) {
      case 1: return t('types.income');
      case 2: return t('types.expense');
      case 3: return t('types.transfer');
      default: return '';
    }
  };

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  useEffect(() => {
    if (isAuthenticated) {
      loadCategories();
    }
  }, [isAuthenticated]);

  const loadCategories = async () => {
    try {
      setLoading(true);
      const categoriesData = await apiClient.getCategories({
        includeSystemCategories: true,
        includeInactive: false,
        includeHierarchy: false
      }) as Category[];
      setCategories(categoriesData || []);
    } catch (error) {
      console.error('Failed to load categories:', error);
      setCategories([]);
    } finally {
      setLoading(false);
    }
  };

  const handleDeleteCategory = async (id: number, name: string) => {
    if (!confirm(`${t('deleteConfirm', { name })}\n\n${t('deleteCannotUndo')}`)) {
      return;
    }

    try {
      await apiClient.deleteCategory(id);
      loadCategories();
    } catch (error: unknown) {
      console.error('Failed to delete category:', error);

      if (error instanceof Error && error.message?.includes('transactions')) {
        toast.error(t('errors.hasTransactions'));
      } else if (error instanceof Error && error.message?.includes('subcategories')) {
        toast.error(t('errors.hasSubcategories'));
      } else {
        toast.error(t('errors.deleteFailed'));
      }
    }
  };

  const handleInitializeCategories = async () => {
    try {
      setInitializingCategories(true);
      const response = await apiClient.initializeDefaultCategories();
      toast.success(response.message);
      loadCategories();
    } catch (error: unknown) {
      console.error('Failed to initialize categories:', error);
      if (error instanceof Error) {
        toast.error(error.message || t('errors.initializeFailed'));
      } else {
        toast.error(t('errors.initializeFailed'));
      }
    } finally {
      setInitializingCategories(false);
    }
  };

  const filteredCategories = categories.filter(category => {
    const matchesSearch = category.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         category.description?.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         category.fullPath.toLowerCase().includes(searchTerm.toLowerCase());

    const matchesType = selectedType === '' || category.type === selectedType;

    return matchesSearch && matchesType;
  });

  // Group categories by type, then sort by hierarchy (parents first, children indented)
  const groupedByType = useMemo(() => {
    const typeOrder = [2, 1, 3]; // Expense, Income, Transfer
    const groups: { type: number; label: string; categories: Category[] }[] = [];

    for (const type of typeOrder) {
      const typeCats = filteredCategories.filter(c => c.type === type);
      if (typeCats.length === 0) continue;

      // Separate parents and children
      const parents = typeCats.filter(c => !c.parentCategoryId).sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name));
      const childrenMap = new Map<number, Category[]>();
      for (const c of typeCats.filter(c => c.parentCategoryId)) {
        const existing = childrenMap.get(c.parentCategoryId!) || [];
        existing.push(c);
        childrenMap.set(c.parentCategoryId!, existing);
      }
      // Sort children within each parent
      for (const [, children] of childrenMap) {
        children.sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name));
      }

      // Build flat list: parent → children → parent → children ...
      const sorted: Category[] = [];
      for (const parent of parents) {
        sorted.push(parent);
        const children = childrenMap.get(parent.id);
        if (children) {
          sorted.push(...children);
        }
      }
      // Also add any orphaned children (whose parent might be filtered out)
      const addedIds = new Set(sorted.map(c => c.id));
      for (const c of typeCats) {
        if (!addedIds.has(c.id)) {
          sorted.push(c);
        }
      }

      groups.push({
        type,
        label: getCategoryTypeLabel(type),
        categories: sorted,
      });
    }

    return groups;
  }, [filteredCategories]);

  const toggleGroup = (type: number) => {
    setCollapsedGroups(prev => {
      const next = new Set(prev);
      if (next.has(type)) {
        next.delete(type);
      } else {
        next.add(type);
      }
      return next;
    });
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <TagIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-slate-700 font-medium">{t('loading')}</div>
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
        <div className="mb-5">
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
            <div>
              <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900 sm:text-[2.1rem]">
                {t('title')}
              </h1>
              <p className="text-[15px] text-slate-500 mt-1.5">
                {t('subtitle')}
              </p>
            </div>

            {activeTab === 'categories' && (
              <div className="flex gap-2">
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => setShowFilters(!showFilters)}
                  className="flex items-center gap-2"
                >
                  <FunnelIcon className="w-4 h-4" />
                  <span className="hidden sm:inline">{tCommon('filters')}</span>
                </Button>

                <Link href="/categories/new">
                  <Button size="sm" className="flex items-center gap-2">
                    <PlusIcon className="w-4 h-4" />
                    <span className="hidden sm:inline">{t('addCategory')}</span>
                    <span className="sm:hidden">{tCommon('add')}</span>
                  </Button>
                </Link>
              </div>
            )}
          </div>

          {/* Tabs */}
          <div className="mt-4 border-b border-slate-200">
            <nav className="-mb-px flex gap-4 sm:gap-6">
              <button
                onClick={() => setActiveTab('categories')}
                className={cn(
                  'flex items-center gap-2 py-3 px-1 border-b-2 font-medium text-sm transition-colors',
                  activeTab === 'categories'
                    ? 'border-violet-500 text-violet-600'
                    : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
                )}
              >
                <TagIcon className="w-5 h-5" />
                <span>{t('tabs.categories')}</span>
              </button>
              <button
                onClick={() => setActiveTab('bank-mappings')}
                className={cn(
                  'flex items-center gap-2 py-3 px-1 border-b-2 font-medium text-sm transition-colors',
                  activeTab === 'bank-mappings'
                    ? 'border-violet-500 text-violet-600'
                    : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
                )}
              >
                <ArrowPathIcon className="w-5 h-5" />
                <span>{t('tabs.bankMappings')}</span>
              </button>
            </nav>
          </div>

          {/* Search Bar - only for categories tab */}
          {activeTab === 'categories' && (
            <div className="mt-4 relative">
              <MagnifyingGlassIcon className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-slate-400" />
              <Input
                type="text"
                placeholder={t('searchPlaceholder')}
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="pl-10 w-full"
              />
            </div>
          )}

          {/* Filters */}
          {activeTab === 'categories' && showFilters && (
            <Card className="mt-4 rounded-[26px] border border-violet-100/80 bg-white/90 shadow-[0_20px_44px_-32px_rgba(76,29,149,0.48)]">
              <CardContent className="p-4">
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-medium text-slate-700 mb-2">
                      {t('categoryType')}
                    </label>
                    <select
                      value={selectedType}
                      onChange={(e) => setSelectedType(e.target.value === '' ? '' : parseInt(e.target.value))}
                      className="select text-sm w-full"
                    >
                      <option value="">{t('allTypes')}</option>
                      <option value="1">{t('types.income')}</option>
                      <option value="2">{t('types.expense')}</option>
                      <option value="3">{t('types.transfer')}</option>
                    </select>
                  </div>
                </div>

                <div className="flex justify-end mt-4 gap-2">
                  <Button
                    variant="secondary"
                    size="sm"
                    onClick={() => {
                      setSelectedType('');
                      setSearchTerm('');
                    }}
                  >
                    {tCommon('clear')}
                  </Button>
                  <Button size="sm" onClick={() => setShowFilters(false)}>
                    {t('applyFilters')}
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}
        </div>

        {/* Bank Mappings Tab */}
        {activeTab === 'bank-mappings' && (
          <BankCategoryMappings />
        )}

        {/* Categories List */}
        {activeTab === 'categories' && (
        <Card className="rounded-[26px] border border-violet-100/60 bg-white/90 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
          <CardContent className="p-0">
            {loading ? (
              <div className="p-6 space-y-4">
                {Array.from({ length: 5 }).map((_, i) => (
                  <div key={i} className="animate-pulse">
                    <div className="flex items-center gap-4 p-4 bg-slate-100 rounded-lg">
                      <div className="w-3 h-10 bg-slate-300 rounded"></div>
                      <div className="flex-1">
                        <div className="h-4 bg-slate-300 rounded w-1/2 mb-2"></div>
                        <div className="h-3 bg-slate-300 rounded w-1/4"></div>
                      </div>
                      <div className="h-6 bg-slate-300 rounded w-20"></div>
                    </div>
                  </div>
                ))}
              </div>
            ) : filteredCategories.length === 0 ? (
              <div className="text-center py-12 px-6">
                <div className="w-20 h-20 bg-gradient-to-br from-primary-400 to-primary-600 rounded-3xl shadow-2xl flex items-center justify-center mx-auto mb-6">
                  <TagIcon className="w-10 h-10 text-white" />
                </div>
                <h3 className="text-xl font-semibold text-slate-900 mb-2">
                  {categories.length === 0 ? t('noCategories') : t('noCategoriesMatch')}
                </h3>
                <p className="text-slate-500 mb-6">
                  {categories.length === 0
                    ? t('getStarted')
                    : t('tryAdjusting')
                  }
                </p>
                {categories.length === 0 && (
                  <div className="flex flex-col sm:flex-row gap-3 justify-center">
                    <Button
                      onClick={handleInitializeCategories}
                      disabled={initializingCategories}
                      className="flex items-center gap-2"
                    >
                      <SparklesIcon className="w-4 h-4" />
                      {initializingCategories ? t('initializing') : t('initializeDefault')}
                    </Button>
                    <Link href="/categories/new">
                      <Button variant="secondary" className="flex items-center gap-2">
                        <PlusIcon className="w-4 h-4" />
                        {t('createCustom')}
                      </Button>
                    </Link>
                  </div>
                )}
              </div>
            ) : (
              <div>
                {groupedByType.map((group) => {
                  const isCollapsed = collapsedGroups.has(group.type);
                  return (
                    <div key={group.type}>
                      {/* Group Header */}
                      <button
                        onClick={() => toggleGroup(group.type)}
                        className="w-full flex items-center justify-between px-5 py-3 bg-slate-50/80 border-b border-slate-100 hover:bg-slate-100/80 transition-colors cursor-pointer"
                      >
                        <div className="flex items-center gap-3">
                          <span className={cn(
                            'inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold',
                            categoryTypeBadgeColors[group.type]
                          )}>
                            {group.label}
                          </span>
                          <span className="text-sm text-slate-500">
                            {t('groupCount', { count: group.categories.length })}
                          </span>
                        </div>
                        <ChevronDownIcon className={cn(
                          'w-4 h-4 text-slate-400 transition-transform',
                          isCollapsed && '-rotate-90'
                        )} />
                      </button>

                      {/* Group Content */}
                      {!isCollapsed && (
                        <div>
                          {group.categories.map((category) => {
                            const isChild = !!category.parentCategoryId;
                            const isExpense = category.type === 2;
                            const isIncome = category.type === 1;

                            return (
                              <div
                                key={category.id}
                                className={cn(
                                  'relative group border-b border-slate-100 last:border-b-0 hover:bg-slate-50 transition-colors',
                                  isChild && 'bg-slate-50/40'
                                )}
                              >
                                <Link href={`/categories/${category.id}`} className="block">
                                  <div className={cn(
                                    'flex items-center gap-3 py-3 pr-12',
                                    isChild ? 'pl-12' : 'pl-5'
                                  )}>
                                    {/* Color indicator */}
                                    <div
                                      className={cn(
                                        'flex-shrink-0 rounded-full',
                                        isChild ? 'w-2 h-2' : 'w-3 h-3'
                                      )}
                                      style={{ backgroundColor: category.color || '#94a3b8' }}
                                    />

                                    {/* Category info */}
                                    <div className="flex-1 min-w-0">
                                      <div className="flex items-center gap-2 flex-wrap">
                                        <h4 className={cn(
                                          'text-slate-900 truncate',
                                          isChild
                                            ? 'text-sm font-medium'
                                            : 'text-base font-semibold'
                                        )}>
                                          {category.name}
                                        </h4>
                                        {category.isSystemCategory && (
                                          <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium bg-slate-100 text-slate-500">
                                            {t('system')}
                                          </span>
                                        )}
                                      </div>
                                      {category.description && (
                                        <p className="text-sm text-slate-500 truncate mt-0.5">
                                          {category.description}
                                        </p>
                                      )}
                                    </div>

                                    {/* Quick stats */}
                                    <div className="flex-shrink-0 text-right">
                                      {category.transactionCount > 0 ? (
                                        <>
                                          <p className={cn(
                                            'font-[var(--font-dash-mono)] text-sm font-semibold',
                                            isIncome ? 'text-emerald-600' : isExpense ? 'text-red-600' : 'text-blue-600'
                                          )}>
                                            {formatCurrency(Math.abs(category.totalAmount))}
                                          </p>
                                          <p className="text-xs text-slate-400">
                                            {t('transactions', { count: category.transactionCount })}
                                          </p>
                                        </>
                                      ) : (
                                        <p className="text-xs text-slate-400">
                                          {t('noTransactions')}
                                        </p>
                                      )}
                                    </div>
                                  </div>
                                </Link>

                                {/* Actions dropdown */}
                                {!category.isSystemCategory && (
                                  <div className="absolute top-3 right-3 z-10">
                                    <DropdownMenu>
                                      <DropdownMenuTrigger asChild>
                                        <Button
                                          variant="ghost"
                                          size="sm"
                                          className="w-8 h-8 p-0 bg-white shadow-sm opacity-0 group-hover:opacity-100 transition-opacity"
                                          onClick={(e) => e.preventDefault()}
                                        >
                                          <EllipsisVerticalIcon className="w-4 h-4" />
                                        </Button>
                                      </DropdownMenuTrigger>
                                      <DropdownMenuContent align="end" className="w-48 bg-white border border-slate-200 shadow-lg z-50">
                                        <DropdownMenuItem asChild>
                                          <Link
                                            href={`/categories/${category.id}`}
                                            className="flex items-center gap-2 cursor-pointer px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 rounded-sm"
                                          >
                                            <EyeIcon className="w-4 h-4" />
                                            {t('viewDetails')}
                                          </Link>
                                        </DropdownMenuItem>
                                        <DropdownMenuItem asChild>
                                          <Link
                                            href={`/categories/${category.id}/edit`}
                                            className="flex items-center gap-2 cursor-pointer px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 rounded-sm"
                                          >
                                            <PencilIcon className="w-4 h-4" />
                                            {t('editCategory')}
                                          </Link>
                                        </DropdownMenuItem>
                                        <DropdownMenuSeparator className="my-1 bg-slate-200" />
                                        <DropdownMenuItem
                                          onClick={() => handleDeleteCategory(category.id, category.name)}
                                          className="flex items-center gap-2 px-3 py-2 text-sm text-red-600 hover:bg-red-50 rounded-sm cursor-pointer"
                                        >
                                          <TrashIcon className="w-4 h-4" />
                                          {t('deleteCategory')}
                                        </DropdownMenuItem>
                                      </DropdownMenuContent>
                                    </DropdownMenu>
                                  </div>
                                )}
                              </div>
                            );
                          })}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </CardContent>
        </Card>
        )}
    </AppLayout>
  );
}
