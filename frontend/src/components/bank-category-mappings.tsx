'use client';

import { useState, useEffect } from 'react';
import { apiClient } from '@/lib/api-client';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { toast } from 'sonner';
import {
  ArrowPathIcon,
  MagnifyingGlassIcon,
  PencilIcon,
  TrashIcon,
  PlusIcon,
  CheckCircleIcon,
  ExclamationCircleIcon,
  SparklesIcon,
  UserIcon,
  AcademicCapIcon,
  NoSymbolIcon,
} from '@heroicons/react/24/outline';
import { BaseModal } from '@/components/modals/base-modal';
import { CategoryPicker } from '@/components/forms/category-picker';
import { useTranslations } from 'next-intl';

interface BankCategoryMapping {
  id: number;
  bankCategoryName: string;
  providerId: string;
  categoryId: number;
  categoryName: string;
  categoryFullPath?: string;
  confidenceScore: number;
  effectiveConfidence: number;
  source: string;
  applicationCount: number;
  overrideCount: number;
  isActive: boolean;
  isExcluded: boolean;
  createdAt: string;
  updatedAt: string;
}

interface MappingStatistics {
  totalMappings: number;
  aiCreatedMappings: number;
  userCreatedMappings: number;
  learnedMappings: number;
  highConfidenceCount: number;
  lowConfidenceCount: number;
  totalApplications: number;
  totalOverrides: number;
}

interface Category {
  id: number;
  name: string;
  fullPath?: string;
  parentId: number | null;
  color?: string;
  icon?: string;
  isSystem?: boolean;
  children?: Category[];
}

export default function BankCategoryMappings() {
  const t = useTranslations('bankConnections');
  const tCommon = useTranslations('common');
  const tToasts = useTranslations('toasts');
  const [mappings, setMappings] = useState<BankCategoryMapping[]>([]);
  const [statistics, setStatistics] = useState<MappingStatistics | null>(null);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [categories, setCategories] = useState<Category[]>([]);
  const [loadingCategories, setLoadingCategories] = useState(true);

  // Edit modal state
  const [editingMapping, setEditingMapping] = useState<BankCategoryMapping | null>(null);
  const [editCategoryId, setEditCategoryId] = useState<number | null>(null);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  // Create modal state
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [newBankCategoryName, setNewBankCategoryName] = useState('');
  const [newCategoryId, setNewCategoryId] = useState<number | null>(null);

  useEffect(() => {
    loadMappings();
    loadCategories();
  }, []);

  const loadMappings = async () => {
    try {
      setLoading(true);
      const response = await apiClient.getBankCategoryMappings({ activeOnly: true });
      setMappings(response.mappings);
      setStatistics(response.statistics);
    } catch (error) {
      console.error('Failed to load bank category mappings:', error);
      toast.error(tToasts('bankCategoryMappingsLoadFailed'));
    } finally {
      setLoading(false);
    }
  };

  const loadCategories = async () => {
    try {
      setLoadingCategories(true);
      const categoriesData = await apiClient.getCategories({
        includeSystemCategories: true,
        includeInactive: false,
        includeHierarchy: true,
      });

      // Flatten hierarchical categories into a flat array for searching
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const flattenCategories = (cats: any[], result: Category[] = []): Category[] => {
        for (const cat of cats) {
          result.push({
            id: cat.id,
            name: cat.name,
            fullPath: cat.fullPath,
            parentId: cat.parentCategoryId ?? null,
            color: cat.color,
            icon: cat.icon,
            isSystem: cat.isSystemCategory,
          });
          if (cat.children && cat.children.length > 0) {
            flattenCategories(cat.children, result);
          }
        }
        return result;
      };

      const mappedCategories = flattenCategories((categoriesData as any[]) || []);
      console.log('[BankCategoryMappings] Loaded categories:', mappedCategories.length, mappedCategories.slice(0, 5));
      setCategories(mappedCategories);
    } catch (error) {
      console.error('Failed to load categories:', error);
    } finally {
      setLoadingCategories(false);
    }
  };

  const handleEditMapping = (mapping: BankCategoryMapping) => {
    setEditingMapping(mapping);
    setEditCategoryId(mapping.categoryId);
    setIsEditModalOpen(true);
  };

  const handleSaveEdit = async () => {
    if (!editingMapping || !editCategoryId) return;

    try {
      setIsSaving(true);
      await apiClient.updateBankCategoryMapping(editingMapping.id, {
        categoryId: editCategoryId,
      });
      toast.success(tToasts('bankCategoryMappingUpdated'));
      setIsEditModalOpen(false);
      setEditingMapping(null);
      loadMappings();
    } catch (error) {
      console.error('Failed to update mapping:', error);
      toast.error(tToasts('bankCategoryMappingUpdateFailed'));
    } finally {
      setIsSaving(false);
    }
  };

  const handleDeleteMapping = async (mapping: BankCategoryMapping) => {
    if (!confirm(t('deleteMappingConfirm', { name: mapping.bankCategoryName }))) {
      return;
    }

    try {
      await apiClient.deleteBankCategoryMapping(mapping.id);
      toast.success(tToasts('bankCategoryMappingDeleted'));
      loadMappings();
    } catch (error) {
      console.error('Failed to delete mapping:', error);
      toast.error(tToasts('bankCategoryMappingDeleteFailed'));
    }
  };

  const handleCreateMapping = async () => {
    if (!newBankCategoryName.trim() || !newCategoryId) {
      toast.error(t('mappingValidation'));
      return;
    }

    try {
      setIsSaving(true);
      await apiClient.createBankCategoryMapping({
        bankCategoryName: newBankCategoryName.trim(),
        categoryId: newCategoryId,
      });
      toast.success(tToasts('bankCategoryMappingCreated'));
      setIsCreateModalOpen(false);
      setNewBankCategoryName('');
      setNewCategoryId(null);
      loadMappings();
    } catch (error) {
      console.error('Failed to create mapping:', error);
      toast.error(tToasts('bankCategoryMappingCreateFailed'));
    } finally {
      setIsSaving(false);
    }
  };

  const handleToggleExclusion = async (mapping: BankCategoryMapping) => {
    try {
      await apiClient.setBankCategoryExclusion(mapping.id, !mapping.isExcluded);
      toast.success(
        mapping.isExcluded
          ? tToasts('bankCategoryMappingIncluded', { name: mapping.bankCategoryName })
          : tToasts('bankCategoryMappingExcluded', { name: mapping.bankCategoryName })
      );
      loadMappings();
    } catch (error) {
      console.error('Failed to toggle exclusion:', error);
      toast.error(tToasts('bankCategoryMappingExclusionFailed'));
    }
  };

  const getSourceIcon = (source: string) => {
    switch (source) {
      case 'AI':
        return <SparklesIcon className="w-4 h-4 text-purple-500" />;
      case 'User':
        return <UserIcon className="w-4 h-4 text-blue-500" />;
      case 'Learned':
        return <AcademicCapIcon className="w-4 h-4 text-green-500" />;
      default:
        return null;
    }
  };

  const getConfidenceColor = (confidence: number) => {
    if (confidence >= 0.9) return 'text-green-600 bg-green-100';
    if (confidence >= 0.7) return 'text-yellow-600 bg-yellow-100';
    return 'text-red-600 bg-red-100';
  };

  const filteredMappings = mappings.filter(mapping =>
    mapping.bankCategoryName.toLowerCase().includes(searchTerm.toLowerCase()) ||
    mapping.categoryName.toLowerCase().includes(searchTerm.toLowerCase()) ||
    (mapping.categoryFullPath?.toLowerCase().includes(searchTerm.toLowerCase()))
  );

  return (
    <div className="space-y-6">
      {/* Statistics Cards */}
      {statistics && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-sm">
            <CardContent className="p-4">
              <div className="text-2xl font-bold text-gray-900">{statistics.totalMappings}</div>
              <div className="text-sm text-gray-600">{t('totalMappings')}</div>
            </CardContent>
          </Card>
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-sm">
            <CardContent className="p-4">
              <div className="text-2xl font-bold text-purple-600">{statistics.aiCreatedMappings}</div>
              <div className="text-sm text-gray-600">{t('aiCreated')}</div>
            </CardContent>
          </Card>
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-sm">
            <CardContent className="p-4">
              <div className="text-2xl font-bold text-green-600">{statistics.highConfidenceCount}</div>
              <div className="text-sm text-gray-600">{t('highConfidence')}</div>
            </CardContent>
          </Card>
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-sm">
            <CardContent className="p-4">
              <div className="text-2xl font-bold text-blue-600">{statistics.totalApplications}</div>
              <div className="text-sm text-gray-600">{t('timesApplied')}</div>
            </CardContent>
          </Card>
        </div>
      )}

      {/* Main Card */}
      <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        <CardHeader className="pb-4">
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
            <CardTitle className="flex items-center gap-2">
              <ArrowPathIcon className="w-6 h-6 text-primary-600" />
              {t('mappingsTitle')}
            </CardTitle>
            <Button
              size="sm"
              onClick={() => setIsCreateModalOpen(true)}
              className="flex items-center gap-2"
              disabled={loadingCategories}
            >
              <PlusIcon className="w-4 h-4" />
              {loadingCategories ? tCommon('loading') : t('addMapping')}
            </Button>
          </div>

          {/* Search */}
          <div className="mt-4 relative">
            <MagnifyingGlassIcon className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-400" />
            <Input
              type="text"
              placeholder={t('searchMappings')}
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="pl-10 w-full"
            />
          </div>
        </CardHeader>

        <CardContent>
          {loading ? (
            <div className="space-y-4">
              {Array.from({ length: 5 }).map((_, i) => (
                <div key={i} className="animate-pulse">
                  <div className="flex items-center gap-4 p-4 bg-gray-100 rounded-lg">
                    <div className="flex-1">
                      <div className="h-4 bg-gray-300 rounded w-1/3 mb-2"></div>
                      <div className="h-3 bg-gray-300 rounded w-1/4"></div>
                    </div>
                    <div className="h-6 bg-gray-300 rounded w-20"></div>
                  </div>
                </div>
              ))}
            </div>
          ) : filteredMappings.length === 0 ? (
            <div className="text-center py-12">
              <div className="w-20 h-20 bg-gradient-to-br from-primary-400 to-primary-600 rounded-3xl shadow-2xl flex items-center justify-center mx-auto mb-6">
                <ArrowPathIcon className="w-10 h-10 text-white" />
              </div>
              <h3 className="text-xl font-semibold text-gray-900 mb-2">
                {mappings.length === 0 ? t('noMappings') : t('noMappingsMatch')}
              </h3>
              <p className="text-gray-600 mb-6">
                {mappings.length === 0
                  ? t('mappingsAutoCreated')
                  : t('adjustSearchTerms')
                }
              </p>
              {mappings.length === 0 && (
                <Button onClick={() => setIsCreateModalOpen(true)} className="flex items-center gap-2 mx-auto">
                  <PlusIcon className="w-4 h-4" />
                  {t('createManualMapping')}
                </Button>
              )}
            </div>
          ) : (
            <div className="space-y-3">
              {filteredMappings.map((mapping) => (
                <div
                  key={mapping.id}
                  className={`flex items-center gap-4 p-4 border rounded-lg transition-colors ${
                    mapping.isExcluded
                      ? 'border-orange-200 bg-orange-50/50 hover:bg-orange-50'
                      : 'border-gray-200 hover:bg-gray-50'
                  }`}
                >
                  {/* Bank Category */}
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span className={`font-medium truncate ${mapping.isExcluded ? 'text-gray-500 line-through' : 'text-gray-900'}`}>
                        {mapping.bankCategoryName}
                      </span>
                      {getSourceIcon(mapping.source)}
                      {mapping.isExcluded && (
                        <span className="px-1.5 py-0.5 bg-orange-100 text-orange-700 text-xs font-medium rounded">
                          {t('excluded')}
                        </span>
                      )}
                    </div>
                    <div className="text-sm text-gray-500 flex items-center gap-1">
                      <span className={`truncate ${mapping.isExcluded ? 'line-through' : ''}`}>
                        {mapping.categoryFullPath || mapping.categoryName}
                      </span>
                    </div>
                  </div>

                  {/* Confidence Badge */}
                  <div className={`px-2 py-1 rounded-full text-xs font-medium ${getConfidenceColor(mapping.effectiveConfidence)}`}>
                    {mapping.effectiveConfidence >= 0.9 ? (
                      <span className="flex items-center gap-1">
                        <CheckCircleIcon className="w-3 h-3" />
                        {Math.round(mapping.effectiveConfidence * 100)}%
                      </span>
                    ) : (
                      <span className="flex items-center gap-1">
                        <ExclamationCircleIcon className="w-3 h-3" />
                        {Math.round(mapping.effectiveConfidence * 100)}%
                      </span>
                    )}
                  </div>

                  {/* Usage Stats */}
                  <div className="hidden sm:block text-sm text-gray-500 text-right min-w-[80px]">
                    <div>{t('usageCount', { count: mapping.applicationCount })}</div>
                    {mapping.overrideCount > 0 && (
                      <div className="text-yellow-600">{t('overrideCount', { count: mapping.overrideCount })}</div>
                    )}
                  </div>

                  {/* Actions */}
                  <div className="flex items-center gap-2">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => handleToggleExclusion(mapping)}
                      className={`w-8 h-8 p-0 ${mapping.isExcluded ? 'text-orange-600 hover:text-orange-700 hover:bg-orange-50' : 'text-gray-400 hover:text-gray-600 hover:bg-gray-100'}`}
                      title={mapping.isExcluded ? t('includeInAutoCategorization') : t('excludeFromAutoCategorization')}
                    >
                      <NoSymbolIcon className="w-4 h-4" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => handleEditMapping(mapping)}
                      className="w-8 h-8 p-0"
                    >
                      <PencilIcon className="w-4 h-4" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => handleDeleteMapping(mapping)}
                      className="w-8 h-8 p-0 text-red-600 hover:text-red-700 hover:bg-red-50"
                    >
                      <TrashIcon className="w-4 h-4" />
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Edit Modal */}
      <BaseModal
        isOpen={isEditModalOpen}
        onClose={() => setIsEditModalOpen(false)}
        title={t('editMapping')}
        size="md"
      >
        {editingMapping && (
          <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                {t('bankCategory')}
              </label>
              <div className="p-3 bg-gray-100 rounded-lg text-gray-900 font-medium">
                {editingMapping.bankCategoryName}
              </div>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                {t('mapToCategory')}
              </label>
              <CategoryPicker
                value={editCategoryId || undefined}
                onChange={(categoryId) => setEditCategoryId(typeof categoryId === 'number' ? categoryId : parseInt(String(categoryId), 10))}
                categories={categories}
                placeholder={t('selectCategory')}
                disableQuickPicks={true}
              />
            </div>
            <div className="flex justify-end gap-3 pt-4">
              <Button variant="secondary" onClick={() => setIsEditModalOpen(false)}>
                {tCommon('cancel')}
              </Button>
              <Button onClick={handleSaveEdit} disabled={isSaving || !editCategoryId}>
                {isSaving ? tCommon('saving') : tCommon('save')}
              </Button>
            </div>
          </div>
        )}
      </BaseModal>

      {/* Create Modal */}
      <BaseModal
        isOpen={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
        title={t('createMapping')}
        size="md"
      >
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              {t('bankCategoryName')}
            </label>
            <Input
              type="text"
              placeholder={t('bankCategoryExample')}
              value={newBankCategoryName}
              onChange={(e) => setNewBankCategoryName(e.target.value)}
            />
            <p className="text-xs text-gray-500 mt-1">
              {t('bankCategoryHelp')}
            </p>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              {t('mapToCategory')}
            </label>
            <CategoryPicker
              value={newCategoryId || undefined}
              onChange={(categoryId) => setNewCategoryId(typeof categoryId === 'number' ? categoryId : parseInt(String(categoryId), 10))}
              categories={categories}
              placeholder={t('selectCategory')}
              disableQuickPicks={true}
            />
          </div>
          <div className="flex justify-end gap-3 pt-4">
            <Button variant="secondary" onClick={() => setIsCreateModalOpen(false)}>
              {tCommon('cancel')}
            </Button>
            <Button
              onClick={handleCreateMapping}
              disabled={isSaving || !newBankCategoryName.trim() || !newCategoryId}
            >
              {isSaving ? tCommon('creating') : tCommon('create')}
            </Button>
          </div>
        </div>
      </BaseModal>
    </div>
  );
}
