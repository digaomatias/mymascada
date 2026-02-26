'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter, useParams } from 'next/navigation';
import { useEffect, useState, useCallback } from 'react';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { apiClient } from '@/lib/api-client';
import Link from 'next/link';
import { 
  ArrowLeftIcon,
  TagIcon,
  CheckIcon
} from '@heroicons/react/24/outline';
import { renderCategoryIcon } from '@/lib/category-icons';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';

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

interface FormData {
  name: string;
  description: string;
  color: string;
  type: number;
  parentCategoryId: number | null;
}

const presetColors = [
  '#EF4444', '#F97316', '#F59E0B', '#EAB308', '#84CC16',
  '#22C55E', '#10B981', '#14B8A6', '#06B6D4', '#0EA5E9',
  '#3B82F6', '#6366F1', '#8B5CF6', '#A855F7', '#D946EF',
  '#EC4899', '#F43F5E'
];

export default function EditCategoryPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const params = useParams();
  const categoryId = params.id as string;
  const t = useTranslations('categories');
  const tCommon = useTranslations('common');
  const tToasts = useTranslations('toasts');

  const [category, setCategory] = useState<Category | null>(null);
  const [categories, setCategories] = useState<Category[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [formData, setFormData] = useState<FormData>({
    name: '',
    description: '',
    color: '#6B7280',
    type: 2,
    parentCategoryId: null
  });
  const [errors, setErrors] = useState<{[K in keyof FormData]?: string}>({});

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  const loadCategoryDetails = useCallback(async () => {
    try {
      setLoading(true);
      const categoryData = await apiClient.getCategory(parseInt(categoryId)) as Category;
      setCategory(categoryData);
      setFormData({
        name: categoryData.name,
        description: categoryData.description || '',
        color: categoryData.color || '#6B7280',
        type: categoryData.type,
        parentCategoryId: categoryData.parentCategoryId || null
      });
    } catch (error) {
      console.error('Failed to load category:', error);
      toast.error(tToasts('categoryLoadFailed'));
      router.push('/categories');
    } finally {
      setLoading(false);
    }
  }, [categoryId, router]);

  const loadCategories = useCallback(async () => {
    try {
      const categoriesData = await apiClient.getCategories({
        includeSystemCategories: true,
        includeInactive: false,
        includeHierarchy: false
      }) as Category[];
      setCategories(categoriesData || []);
    } catch (error) {
      console.error('Failed to load categories:', error);
      setCategories([]);
    }
  }, []);

  useEffect(() => {
    if (isAuthenticated && categoryId) {
      loadCategoryDetails();
      loadCategories();
    }
  }, [isAuthenticated, categoryId, loadCategoryDetails, loadCategories]);

  const handleInputChange = (field: keyof FormData, value: string | number | null) => {
    setFormData(prev => ({ ...prev, [field]: value }));
    if (errors[field]) {
      setErrors(prev => ({ ...prev, [field]: undefined }));
    }
  };

  const validateForm = (): boolean => {
    const newErrors: {[K in keyof FormData]?: string} = {};

    if (!formData.name.trim()) {
      newErrors.name = t('validation.nameRequired');
    }

    if (formData.parentCategoryId !== null && formData.parentCategoryId.toString() === categoryId) {
      newErrors.parentCategoryId = t('validation.cannotBeSelfParent');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!validateForm() || !category) {
      return;
    }

    setSaving(true);
    try {
      await apiClient.updateCategory(parseInt(categoryId), {
        id: parseInt(categoryId),
        name: formData.name.trim(),
        description: formData.description.trim() || undefined,
        color: formData.color,
        icon: category.icon,
        type: formData.type,
        parentCategoryId: formData.parentCategoryId || undefined,
        sortOrder: category.sortOrder,
        isActive: category.isActive
      });
      
      toast.success(tToasts('categoryUpdated'));
      router.push(`/categories/${categoryId}`);
    } catch (error) {
      console.error('Failed to update category:', error);
      toast.error(tToasts('categoryUpdateFailed'));
    } finally {
      setSaving(false);
    }
  };

  if (isLoading || loading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <TagIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">{t('details.loadingCategory')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated || !category) {
    return null;
  }

  if (category.isSystemCategory) {
    return (
      <AppLayout>
          <div className="text-center">
            <h1 className="text-2xl font-bold text-gray-900 mb-4">{t('edit.cannotEditSystem')}</h1>
            <p className="text-gray-600 mb-6">{t('edit.systemCategoriesCannotBeModified')}</p>
            <Link href={`/categories/${categoryId}`}>
              <Button>{t('edit.backToCategory')}</Button>
            </Link>
          </div>
      </AppLayout>
    );
  }

  // Filter available parent categories (exclude self and descendants)
  const availableParentCategories = categories.filter(cat => 
    cat.id !== parseInt(categoryId) && 
    !cat.fullPath.startsWith(category.fullPath)
  );

  return (
    <AppLayout>
        {/* Header */}
        <div className="mb-6 lg:mb-8">
          {/* Navigation Bar */}
          <div className="flex items-center justify-between mb-6">
            <Link href={`/categories/${categoryId}`}>
              <Button variant="secondary" size="sm" className="flex items-center gap-2">
                <ArrowLeftIcon className="w-4 h-4" />
                <span className="hidden sm:inline">{t('edit.backToCategory')}</span>
                <span className="sm:hidden">{tCommon('back')}</span>
              </Button>
            </Link>
          </div>

          {/* Page Title */}
          <div className="text-center mb-8">
            <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900 mb-2">
              {t('edit.title')}
            </h1>
            <p className="text-gray-600 text-sm sm:text-base">
              {t('edit.subtitle')}
            </p>
          </div>
        </div>

        <div className="max-w-2xl mx-auto">
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardHeader>
              <CardTitle className="text-xl font-bold text-gray-900 flex items-center gap-2">
                <TagIcon className="w-6 h-6 text-primary-600" />
                {t('categoryDetails')}
              </CardTitle>
            </CardHeader>
            
            <CardContent>
              <form onSubmit={handleSubmit} className="space-y-6">
                {/* Preview */}
                <div className="text-center p-6 bg-gray-50 rounded-lg">
                  <div 
                    className="w-16 h-16 rounded-2xl shadow-lg flex items-center justify-center mx-auto mb-3 transition-colors"
                    style={{ backgroundColor: formData.color }}
                  >
                    {renderCategoryIcon(category.icon, "w-8 h-8 text-white")}
                  </div>
                  <h3 className="text-lg font-semibold text-gray-900">
                    {formData.name || 'Category Name'}
                  </h3>
                  {formData.description && (
                    <p className="text-sm text-gray-600 mt-1">{formData.description}</p>
                  )}
                </div>

                {/* Name */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    {t('edit.categoryName')}
                  </label>
                  <Input
                    type="text"
                    value={formData.name}
                    onChange={(e) => handleInputChange('name', e.target.value)}
                    placeholder={t('edit.categoryNamePlaceholder')}
                    className={errors.name ? 'border-red-500' : ''}
                  />
                  {errors.name && (
                    <p className="mt-1 text-sm text-red-600">{errors.name}</p>
                  )}
                </div>

                {/* Description */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    {t('edit.description')}
                  </label>
                  <Input
                    type="text"
                    value={formData.description}
                    onChange={(e) => handleInputChange('description', e.target.value)}
                    placeholder={t('edit.descriptionPlaceholder')}
                  />
                </div>

                {/* Parent Category */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    {t('edit.parentCategory')}
                  </label>
                  <select
                    value={formData.parentCategoryId || ''}
                    onChange={(e) => handleInputChange('parentCategoryId', e.target.value ? parseInt(e.target.value) : null)}
                    className={`select ${errors.parentCategoryId ? 'border-red-500' : ''}`}
                  >
                    <option value="">{t('edit.noParent')}</option>
                    {availableParentCategories.map((cat) => (
                      <option key={cat.id} value={cat.id}>
                        {cat.fullPath}
                      </option>
                    ))}
                  </select>
                  {errors.parentCategoryId && (
                    <p className="mt-1 text-sm text-red-600">{errors.parentCategoryId}</p>
                  )}
                </div>

                {/* Color */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    {t('edit.color')}
                  </label>
                  <div className="flex items-center gap-3 mb-3">
                    <input
                      type="color"
                      value={formData.color}
                      onChange={(e) => handleInputChange('color', e.target.value)}
                      className="w-12 h-12 rounded-lg border border-gray-300 cursor-pointer"
                    />
                    <Input
                      type="text"
                      value={formData.color}
                      onChange={(e) => handleInputChange('color', e.target.value)}
                      placeholder="#000000"
                      className="flex-1"
                    />
                  </div>
                  
                  {/* Preset Colors */}
                  <div className="grid grid-cols-8 sm:grid-cols-12 gap-2">
                    {presetColors.map((color) => (
                      <button
                        key={color}
                        type="button"
                        onClick={() => handleInputChange('color', color)}
                        className={`w-8 h-8 rounded-lg border-2 transition-all ${
                          formData.color === color 
                            ? 'border-gray-900 scale-110' 
                            : 'border-gray-300 hover:border-gray-400'
                        }`}
                        style={{ backgroundColor: color }}
                      />
                    ))}
                  </div>
                </div>

                {/* Submit Buttons */}
                <div className="flex gap-3 pt-6">
                  <Link href={`/categories/${categoryId}`} className="flex-1">
                    <Button variant="secondary" className="w-full">
                      {tCommon('cancel')}
                    </Button>
                  </Link>
                  <Button
                    type="submit"
                    disabled={saving}
                    className="flex-1 flex items-center justify-center gap-2"
                  >
                    {saving ? (
                      <>
                        <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                        {t('edit.updating')}
                      </>
                    ) : (
                      <>
                        <CheckIcon className="w-4 h-4" />
                        {t('edit.updateCategory')}
                      </>
                    )}
                  </Button>
                </div>
              </form>
            </CardContent>
          </Card>
        </div>
    </AppLayout>
  );
}
