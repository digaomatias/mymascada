'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { apiClient } from '@/lib/api-client';
import Link from 'next/link';
import {
  ArrowLeftIcon,
  TagIcon,
  CheckIcon,
  ExclamationTriangleIcon
} from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';

interface Category {
  id: number;
  name: string;
  color?: string;

  isSystemCategory?: boolean;
  fullPath?: string;
}

export default function NewCategoryPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const t = useTranslations('categories');
  const tCommon = useTranslations('common');
  const tToasts = useTranslations('toasts');
  const [loading, setLoading] = useState(false);
  const [categories, setCategories] = useState<Category[]>([]);
  const [errors, setErrors] = useState<{ [key: string]: string }>({});
  const [success, setSuccess] = useState(false);

  // Form state
  const [formData, setFormData] = useState({
    name: '',
    description: '',
    color: '#6B7280',

    parentCategoryId: '',
  });

  const colorOptions = [
    '#6B7280', '#EF4444', '#F59E0B', '#10B981', '#3B82F6', 
    '#8B5CF6', '#EC4899', '#F97316', '#84CC16', '#06B6D4'
  ];

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
      const categoriesData = await apiClient.getCategories() as Category[];
      setCategories(categoriesData || []);
    } catch (error) {
      console.error('Failed to load categories:', error);
      setCategories([]);
    }
  };

  const validateForm = () => {
    const newErrors: { [key: string]: string } = {};

    if (!formData.name.trim()) {
      newErrors.name = t('validation.pleaseEnterName');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!validateForm()) {
      return;
    }

    try {
      setLoading(true);
      setErrors({});

      const categoryData = {
        name: formData.name.trim(),
        description: formData.description.trim() || undefined,
        color: formData.color,

        parentCategoryId: formData.parentCategoryId ? parseInt(formData.parentCategoryId) : undefined,
        sortOrder: 999,
      };

      await apiClient.createCategory(categoryData);
      
      setSuccess(true);
      
      // Redirect after a brief success message
      setTimeout(() => {
        router.push('/categories');
      }, 1500);

    } catch (error: unknown) {
      console.error('Failed to create category:', error);
      setErrors({
        submit: error instanceof Error ? error.message : tToasts('categoryCreateFailed')
      });
    } finally {
      setLoading(false);
    }
  };

  const handleInputChange = (field: string, value: string | number) => {
    setFormData(prev => ({ ...prev, [field]: value }));
    
    // Clear error when user starts typing
    if (errors[field]) {
      setErrors(prev => ({ ...prev, [field]: '' }));
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <TagIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">{tCommon('loading')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return null;
  }

  if (success) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <Card className="mx-4 max-w-md w-full bg-white/90 backdrop-blur-xs border-0 shadow-2xl">
          <CardContent className="p-8 text-center">
            <div className="w-16 h-16 bg-gradient-to-br from-success-500 to-success-600 rounded-2xl shadow-2xl flex items-center justify-center mx-auto mb-6">
              <CheckIcon className="w-8 h-8 text-white" />
            </div>
            <h2 className="text-2xl font-bold text-gray-900 mb-2">{t('new.categoryCreated')}</h2>
            <p className="text-gray-600 mb-6">{t('new.categoryCreatedDesc')}</p>
            <div className="text-sm text-gray-500">{t('new.redirectingToCategories')}</div>
          </CardContent>
        </Card>
      </div>
    );
  }

  // Filter parent categories (only top-level categories)
  const parentCategoryOptions = categories.filter(cat => !cat.isSystemCategory);

  return (
    <AppLayout>
        {/* Header */}
        <div className="mb-6 lg:mb-8">
          {/* Navigation Bar */}
          <div className="flex items-center justify-between mb-6">
            <Link href="/categories">
              <Button variant="secondary" size="sm" className="flex items-center gap-2">
                <ArrowLeftIcon className="w-4 h-4" />
                <span className="hidden sm:inline">{t('details.backToCategories')}</span>
                <span className="sm:hidden">{tCommon('back')}</span>
              </Button>
            </Link>
          </div>

          {/* Page Title */}
          <div className="text-center mb-8">
            <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900 mb-2">
              {t('new.title')}
            </h1>
            <p className="text-gray-600 text-sm sm:text-base">
              {t('new.subtitle')}
            </p>
          </div>
        </div>

        {/* Category Form */}
        <div className="max-w-2xl mx-auto">
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <TagIcon className="w-6 h-6 text-primary-600" />
                {t('categoryDetails')}
              </CardTitle>
            </CardHeader>
            
            <CardContent>
              <form onSubmit={handleSubmit} className="space-y-6">
                {/* General Error */}
                {errors.submit && (
                  <div className="bg-red-50 border border-red-200 rounded-lg p-4 flex items-start gap-3">
                    <ExclamationTriangleIcon className="w-5 h-5 text-red-500 flex-shrink-0 mt-0.5" />
                    <div>
                      <h4 className="text-sm font-medium text-red-800">{tCommon('error')}</h4>
                      <p className="text-sm text-red-700 mt-1">{errors.submit}</p>
                    </div>
                  </div>
                )}

                {/* Name Field */}
                <div>
                  <label htmlFor="name" className="block text-sm font-medium text-gray-700 mb-2">
                    {t('edit.categoryName')}
                  </label>
                  <Input
                    id="name"
                    type="text"
                    placeholder={t('new.categoryNamePlaceholder')}
                    value={formData.name}
                    onChange={(e) => handleInputChange('name', e.target.value)}
                    className={errors.name ? 'border-red-300 focus:border-red-500 focus:ring-red-500' : ''}
                  />
                  {errors.name && (
                    <p className="mt-1 text-sm text-red-600">{errors.name}</p>
                  )}
                </div>

                {/* Parent Category Field */}
                {parentCategoryOptions.length > 0 && (
                  <div>
                    <label htmlFor="parentCategoryId" className="block text-sm font-medium text-gray-700 mb-2">
                      {t('new.parentCategoryOptional')}
                    </label>
                    <select
                      id="parentCategoryId"
                      value={formData.parentCategoryId}
                      onChange={(e) => handleInputChange('parentCategoryId', e.target.value)}
                      className="select"
                    >
                      <option value="">{t('new.noParent')}</option>
                      {parentCategoryOptions.map((category) => (
                        <option key={category.id} value={category.id}>
                          {category.name}
                        </option>
                      ))}
                    </select>
                    <p className="mt-1 text-xs text-gray-500">
                      {t('new.parentHelp')}
                    </p>
                  </div>
                )}

                {/* Color Field */}
                <div>
                  <label htmlFor="color" className="block text-sm font-medium text-gray-700 mb-2">
                    {t('edit.color')}
                  </label>
                  <div className="flex gap-2 flex-wrap">
                    {colorOptions.map((color) => (
                      <button
                        key={color}
                        type="button"
                        onClick={() => handleInputChange('color', color)}
                        className={`w-8 h-8 rounded-full border-2 ${
                          formData.color === color ? 'border-gray-800' : 'border-gray-300'
                        }`}
                        style={{ backgroundColor: color }}
                        aria-label={`Select color ${color}`}
                      />
                    ))}
                  </div>
                  <Input
                    type="text"
                    value={formData.color}
                    onChange={(e) => handleInputChange('color', e.target.value)}
                    placeholder="#6B7280"
                    className="mt-2 w-32"
                  />
                </div>


                {/* Description Field */}
                <div>
                  <label htmlFor="description" className="block text-sm font-medium text-gray-700 mb-2">
                    {t('new.descriptionLabel')}
                  </label>
                  <textarea
                    id="description"
                    rows={3}
                    placeholder={t('new.descriptionPlaceholder')}
                    value={formData.description}
                    onChange={(e) => handleInputChange('description', e.target.value)}
                    className="w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 resize-none"
                  />
                </div>

                {/* Submit Buttons */}
                <div className="flex flex-col sm:flex-row gap-3 pt-4 pb-4 md:pb-0">
                  <Link href="/categories" className="flex-1">
                    <Button
                      type="button"
                      variant="secondary"
                      className="w-full"
                      disabled={loading}
                    >
                      {tCommon('cancel')}
                    </Button>
                  </Link>

                  <Button
                    type="submit"
                    className="flex-1 sm:flex-2"
                    disabled={loading}
                  >
                    {loading ? (
                      <div className="flex items-center gap-2">
                        <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                        {t('new.creating')}
                      </div>
                    ) : (
                      t('new.createCategory')
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

// Force dynamic rendering for this page
export const dynamic = 'force-dynamic';