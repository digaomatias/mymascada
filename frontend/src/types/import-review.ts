// Import Review System Type Definitions

// Base Transaction interface (matching backend Transaction entity structure)
export interface Transaction {
  id?: number;
  amount: number;
  transactionDate: string;
  description: string;
  userDescription?: string;
  status: TransactionStatus;
  source: TransactionSource;
  externalId?: string;
  referenceNumber?: string;
  notes?: string;
  location?: string;
  isReviewed: boolean;
  isExcluded: boolean;
  tags?: string;
  accountId: number;
  categoryId?: number;
  relatedTransactionId?: number;
  type: TransactionType;
  transferId?: string;
  isTransferSource: boolean;
  createdAt: string;
  updatedAt: string;
  isDeleted: boolean;
}

// Enums (matching backend enums)
export enum TransactionStatus {
  Pending = 1,
  Cleared = 2,
  Reconciled = 3,
  Cancelled = 4
}

export enum TransactionSource {
  Manual = 1,
  CsvImport = 2,
  OfxImport = 3,
  ApiImport = 4
}

export enum TransactionType {
  Debit = 1,
  Credit = 2,
  TransferComponent = 3
}

// Import Review Core Types
export interface ImportCandidate {
  // Proposed transaction data (before import)
  amount: number;
  date: string;  // Changed from transactionDate to match backend
  description: string;
  referenceNumber?: string;
  externalId?: string;
  source: TransactionSource;
  type?: TransactionType;
  categoryId?: number;
  notes?: string;
  
  // Import metadata
  sourceRowIndex: number;
  csvRowData?: Record<string, string>; // Original CSV row data for reference
  confidence: number; // 0-100, confidence in data quality
}

export interface ConflictInfo {
  type: ConflictType;
  severity: ConflictSeverity;
  message: string;
  conflictingTransaction?: ExistingTransaction;
  confidenceScore: number; // 0-1 decimal, matching backend
}

export enum ConflictType {
  None = 0,
  ExactDuplicate = 1,
  PotentialDuplicate = 2, 
  TransferConflict = 3,
  ManualEntryConflict = 4,
  AmountMismatch = 5,
  DateMismatch = 6,
  CategoryConflict = 7
}

export enum ConflictSeverity {
  Low = 0,
  Medium = 1,
  High = 2,
  Critical = 3
}

export enum ConflictReason {
  SameExternalId = 'same_external_id',
  SameReferenceNumber = 'same_reference_number',
  SameAmountAndDate = 'same_amount_and_date',
  SimilarAmountNearDate = 'similar_amount_near_date',
  TransferDestinationExists = 'transfer_destination_exists',
  ManualEntryMatch = 'manual_entry_match',
  SimilarDescription = 'similar_description'
}

export enum ConflictResolution {
  Pending = 0,
  Import = 1,
  Skip = 2, 
  MergeWithExisting = 3,
  ReplaceExisting = 4
}

export interface ExistingTransaction {
  id: number;
  amount: number;
  transactionDate: string;
  description: string;
  referenceId?: string;
  externalReferenceId?: string;
  source: TransactionSource;
  status: TransactionStatus;
  createdAt: string;
}

export interface ImportReviewItem {
  id: string; // Unique identifier for this review item
  importCandidate: ImportCandidate;
  conflicts: ConflictInfo[];
  reviewDecision: ConflictResolution;
  userNotes?: string;
  isProcessed: boolean;
}

// Import Analysis Response
export interface ImportAnalysisResult {
  success: boolean;
  accountId: number;
  importSource: TransactionSource;
  
  // Review items organized by type
  reviewItems: ImportReviewItem[];
  
  // Summary statistics
  summary: {
    totalCandidates: number;
    cleanImports: number;
    exactDuplicates: number;
    potentialDuplicates: number;
    transferConflicts: number;
    manualConflicts: number;
    requiresReview: number;
  };
  
  // Analysis metadata
  analysisId: string;
  analysisTimestamp?: string; // For backward compatibility
  warnings: string[];
  errors: string[];
}

// Import Execution Request
export interface ImportExecutionRequest {
  analysisId: string; // Reference to the analysis result
  accountId: number;
  decisions: ImportDecision[];
}

export interface ImportDecision {
  reviewItemId: string;
  decision: ConflictResolution; // Numeric enum value (0-4)
  userNotes?: string;
  candidate?: ImportCandidate; // Include candidate data to avoid cache dependency
}

// Import Execution Response
export interface ImportExecutionResult {
  success: boolean;
  message: string;
  
  // Standard counts (consistent with existing import responses)
  importedTransactionsCount: number;
  skippedTransactionsCount: number;
  duplicateTransactionsCount: number;
  mergedTransactionsCount: number;
  
  // Detailed results
  processedItems: ProcessedImportItem[];
  warnings: string[];
  errors: string[];
  
  // Account info
  targetAccountId: number;
  createdAccountId?: number;
}

export interface ProcessedImportItem {
  reviewItemId: string;
  action: ConflictResolution;
  success: boolean;
  createdTransactionId?: number;
  updatedTransactionId?: number;
  error?: string;
}

// Bulk Action Types
export interface BulkActionRequest {
  reviewItemIds: string[];
  action: BulkAction;
  criteria?: BulkActionCriteria;
}

export enum BulkAction {
  SkipAllExactDuplicates = 'skip_exact_duplicates',
  ImportAllClean = 'import_clean',
  SkipAllLowConfidence = 'skip_low_confidence',
  ImportAllHighConfidence = 'import_high_confidence',
  MarkAllAsDifferent = 'mark_all_different'
}

export interface BulkActionCriteria {
  conflictTypes?: ConflictType[];
  minConfidence?: number;
  maxConfidence?: number;
  amountRange?: { min: number; max: number };
  dateRange?: { start: string; end: string };
}

// UI Component Props
export interface ImportReviewScreenProps {
  analysisResult: ImportAnalysisResult;
  onImportComplete: (result: ImportExecutionResult) => void;
  onCancel: () => void;
  accountName?: string;
  showBulkActions?: boolean;
}

export interface ConflictResolutionCardProps {
  reviewItem: ImportReviewItem;
  onDecisionChange: (itemId: string, decision: ConflictResolution, notes?: string) => void;
  isReadOnly?: boolean;
  showDetails?: boolean;
}

// Helper Types for API Integration
export interface AnalyzeImportRequest {
  source: TransactionSource;
  accountId: number;
  
  // For CSV imports
  csvData?: {
    content: string; // Base64 encoded
    mappings: Record<string, string>;
    hasHeader: boolean;
  };
  
  // For OFX imports  
  ofxData?: {
    content: string; // Base64 encoded
    createAccount?: boolean;
    accountName?: string;
  };
  
  // Analysis options
  options?: {
    dateToleranceDays?: number;
    amountTolerance?: number;
    enableTransferDetection?: boolean;
    conflictDetectionLevel?: 'strict' | 'moderate' | 'relaxed';
  };
}

// Export default collection for easy importing
export const ImportReviewTypes = {
  ConflictType,
  ConflictReason, 
  ConflictResolution,
  BulkAction,
  TransactionStatus,
  TransactionSource,
  TransactionType
} as const;