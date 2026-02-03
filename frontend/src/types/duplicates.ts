export interface DuplicateTransactionDto {
  id: number;
  amount: number;
  transactionDate: string;
  description: string;
  userDescription?: string;
  accountId: number;
  accountName: string;
  categoryId?: number;
  categoryName?: string;
  categoryColor?: string;
  status: number;
  source: number;
  isReviewed: boolean;
  externalId?: string;
  notes?: string;
  location?: string;
  tags?: string;
  type: number;
  createdAt: string;
  updatedAt: string;
}

export interface DuplicateGroupDto {
  id: string;
  transactions: DuplicateTransactionDto[];
  highestConfidence: number;
  totalAmount: number;
  dateRange: string;
  description: string;
}

export interface DuplicateTransactionsResponse {
  duplicateGroups: DuplicateGroupDto[];
  totalGroups: number;
  totalTransactions: number;
  processedAt: string;
}

export interface DuplicateDetectionParams {
  amountTolerance?: number;
  dateToleranceDays?: number;
  includeReviewed?: boolean;
  sameAccountOnly?: boolean;
  minConfidence?: number;
}