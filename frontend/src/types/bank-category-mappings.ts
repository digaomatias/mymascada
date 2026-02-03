export interface BankCategoryMapping {
  id: number;
  bankCategoryName: string;
  providerId: string;
  categoryId: number;
  categoryName: string;
  categoryFullPath?: string;
  confidenceScore: number;
  effectiveConfidence: number;
  source: 'AI' | 'User' | 'Learned';
  applicationCount: number;
  overrideCount: number;
  isActive: boolean;
  isExcluded: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface MappingStatistics {
  totalMappings: number;
  aiCreatedMappings: number;
  userCreatedMappings: number;
  learnedMappings: number;
  highConfidenceCount: number;
  lowConfidenceCount: number;
  totalApplications: number;
  totalOverrides: number;
}

export interface BankCategoryMappingsListResponse {
  mappings: BankCategoryMapping[];
  totalCount: number;
  statistics: MappingStatistics;
}

export interface CreateBankCategoryMappingRequest {
  bankCategoryName: string;
  providerId?: string;
  categoryId: number;
}

export interface UpdateBankCategoryMappingRequest {
  categoryId: number;
}
