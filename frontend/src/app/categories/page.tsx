'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';
import dynamic from 'next/dynamic';
import Navigation from '@/components/navigation';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
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
            <div className="h-24 bg-gray-100 rounded-lg"></div>
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
import { renderCategoryIcon } from '@/lib/category-icons';
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

// Category type labels will be translated using t('types.income'), etc.

const categoryTypeColors = {
  1: 'bg-green-100 text-green-800',
  2: 'bg-red-100 text-red-800',
  3: 'bg-blue-100 text-blue-800'
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

  // Helper function to get translated category type
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
      loadCategories(); // Refresh the list
    } catch (error: unknown) {
      console.error('Failed to delete category:', error);

      // Handle specific error cases
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
      loadCategories(); // Refresh the list to show the new categories
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

  if (isLoading) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-primary-500 to-primary-700 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <TagIcon className="w-8 h-8 text-white" />
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
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
      <Navigation />
      
      <main className="container-responsive py-4 sm:py-6 lg:py-8">
        {/* Header */}
        <div className="mb-6 lg:mb-8">
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
            <div>
              <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900">
                {t('title')}
              </h1>
              <p className="text-gray-600 mt-1">
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
          <div className="mt-4 border-b border-gray-200">
            <nav className="-mb-px flex gap-4 sm:gap-6">
              <button
                onClick={() => setActiveTab('categories')}
                className={`flex items-center gap-2 py-3 px-1 border-b-2 font-medium text-sm transition-colors ${
                  activeTab === 'categories'
                    ? 'border-primary-500 text-primary-600'
                    : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                }`}
              >
                <TagIcon className="w-5 h-5" />
                <span>{t('tabs.categories')}</span>
              </button>
              <button
                onClick={() => setActiveTab('bank-mappings')}
                className={`flex items-center gap-2 py-3 px-1 border-b-2 font-medium text-sm transition-colors ${
                  activeTab === 'bank-mappings'
                    ? 'border-primary-500 text-primary-600'
                    : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                }`}
              >
                <ArrowPathIcon className="w-5 h-5" />
                <span>{t('tabs.bankMappings')}</span>
              </button>
            </nav>
          </div>

          {/* Search Bar - only for categories tab */}
          {activeTab === 'categories' && (
            <div className="mt-4 relative">
              <MagnifyingGlassIcon className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-400" />
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
            <Card className="mt-4 bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardContent className="p-4">
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-2">
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
        <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <TagIcon className="w-6 h-6 text-primary-600" />
              {t('yourCategoriesCount', { count: filteredCategories.length })}
            </CardTitle>
          </CardHeader>

          <CardContent>
            {loading ? (
              <div className="space-y-4">
                {Array.from({ length: 5 }).map((_, i) => (
                  <div key={i} className="animate-pulse">
                    <div className="flex items-center gap-4 p-4 bg-gray-100 rounded-lg">
                      <div className="w-12 h-12 bg-gray-300 rounded-xl"></div>
                      <div className="flex-1">
                        <div className="h-4 bg-gray-300 rounded w-1/2 mb-2"></div>
                        <div className="h-3 bg-gray-300 rounded w-1/4"></div>
                      </div>
                      <div className="h-6 bg-gray-300 rounded w-20"></div>
                    </div>
                  </div>
                ))}
              </div>
            ) : filteredCategories.length === 0 ? (
              <div className="text-center py-12">
                <div className="w-20 h-20 bg-gradient-to-br from-primary-400 to-primary-600 rounded-3xl shadow-2xl flex items-center justify-center mx-auto mb-6">
                  <TagIcon className="w-10 h-10 text-white" />
                </div>
                <h3 className="text-xl font-semibold text-gray-900 mb-2">
                  {categories.length === 0 ? t('noCategories') : t('noCategoriesMatch')}
                </h3>
                <p className="text-gray-600 mb-6">
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
              <div className="space-y-3 sm:space-y-4">
                {filteredCategories.map((category) => (
                  <div key={category.id} className="relative group p-3 sm:p-4 border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors">
                    {/* Clickable category row */}
                    <Link href={`/categories/${category.id}`} className="block">
                      <div className="flex items-start sm:items-center gap-3 sm:gap-4">
                        {/* Category Icon */}
                        <div 
                          className="w-12 h-12 sm:w-14 sm:h-14 rounded-xl flex items-center justify-center shadow-sm flex-shrink-0"
                          style={{ backgroundColor: category.color || '#6B7280' }}
                        >
                          {renderCategoryIcon(category.icon, "w-6 h-6 sm:w-7 sm:h-7 text-white")}
                        </div>
                        
                        {/* Category Details */}
                        <div className="flex-1 min-w-0">
                          <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-2">
                            <div className="min-w-0 flex-1">
                              {/* Category name with path on mobile */}
                              <div className="flex items-center gap-2 flex-wrap">
                                <h4 className="text-base sm:text-lg font-semibold text-gray-900">
                                  {category.name}
                                </h4>
                                {/* Show path on mobile as part of title */}
                                <span className="text-sm text-gray-500 sm:hidden">
                                  {category.fullPath}
                                </span>
                              </div>
                              
                              {/* Tags and metadata */}
                              <div className="flex items-center gap-2 mt-1 flex-wrap">
                                <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${categoryTypeColors[category.type as keyof typeof categoryTypeColors]}`}>
                                  {getCategoryTypeLabel(category.type)}
                                </span>
                                {category.isSystemCategory && (
                                  <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
                                    {t('system')}
                                  </span>
                                )}
                                {category.parentCategoryName && (
                                  <span className="text-xs sm:text-sm text-gray-500">
                                    {t('in', { parent: category.parentCategoryName })}
                                  </span>
                                )}
                              </div>
                              
                              {/* Description - show full on mobile, truncate on desktop */}
                              {category.description && (
                                <p className="text-sm text-gray-600 mt-1 sm:truncate">
                                  {category.description}
                                </p>
                              )}
                            </div>
                            
                            {/* Actions and desktop path */}
                            <div className="flex items-center gap-3 sm:ml-4">
                              {/* Path - only show on desktop, with space for dropdown */}
                              <div className="hidden sm:block text-right pr-10">
                                <p className="text-sm text-gray-500">
                                  {category.fullPath}
                                </p>
                              </div>
                            </div>
                          </div>
                        </div>
                      </div>
                    </Link>

                    {/* Actions dropdown - positioned absolutely to avoid interfering with row click */}
                    {!category.isSystemCategory && (
                      <div className="absolute top-3 right-3 sm:top-4 sm:right-4 z-10">
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button 
                              variant="ghost" 
                              size="sm" 
                              className="w-8 h-8 p-0 bg-white shadow-sm opacity-0 group-hover:opacity-100 transition-opacity"
                              onClick={(e) => e.preventDefault()} // Prevent row click
                            >
                              <EllipsisVerticalIcon className="w-4 h-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end" className="w-48 bg-white border border-gray-200 shadow-lg z-50">
                            <DropdownMenuItem asChild>
                              <Link
                                href={`/categories/${category.id}`}
                                className="flex items-center gap-2 cursor-pointer px-3 py-2 text-sm text-gray-700 hover:bg-gray-50 rounded-sm"
                              >
                                <EyeIcon className="w-4 h-4" />
                                {t('viewDetails')}
                              </Link>
                            </DropdownMenuItem>
                            <DropdownMenuItem asChild>
                              <Link
                                href={`/categories/${category.id}/edit`}
                                className="flex items-center gap-2 cursor-pointer px-3 py-2 text-sm text-gray-700 hover:bg-gray-50 rounded-sm"
                              >
                                <PencilIcon className="w-4 h-4" />
                                {t('editCategory')}
                              </Link>
                            </DropdownMenuItem>
                            <DropdownMenuSeparator className="my-1 bg-gray-200" />
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
                ))}
              </div>
            )}
          </CardContent>
        </Card>
        )}
      </main>
    </div>
  );
}