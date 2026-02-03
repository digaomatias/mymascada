export interface PotentialTransfersResponse {
  transferGroups: TransferGroup[];
  unmatchedTransfers: UnmatchedTransfer[];
  totalGroups: number;
  totalUnmatched: number;
  processedAt: string;
}

export interface TransferGroup {
  id: string;
  sourceTransaction: TransferTransaction;
  destinationTransaction: TransferTransaction;
  confidence: number;
  amount: number;
  dateRange: string;
  isConfirmed: boolean;
  matchReasons: string[];
}

export interface UnmatchedTransfer {
  transaction: TransferTransaction;
  transferConfidence: number;
  suggestedDestinationAccountId?: number;
  suggestedDestinationAccountName?: string;
  transferIndicators: string[];
}

export interface TransferTransaction {
  id: number;
  amount: number;
  transactionDate: string;
  description: string;
  userDescription?: string;
  accountId: number;
  accountName?: string;
  categoryId?: number;
  categoryName?: string;
  status: number;
  type: number;
  transferId?: string;
  isTransferSource?: boolean;
  relatedTransactionId?: number;
}

export interface TransferDetectionParams {
  amountTolerance: number;
  dateToleranceDays: number;
  includeReviewed: boolean;
  minConfidence: number;
  includeExistingTransfers: boolean;
}

export interface ConfirmTransferMatchRequest {
  groupId: string;
  sourceTransactionId: number;
  destinationTransactionId: number;
  description?: string;
  notes?: string;
}

export interface CreateMissingTransferRequest {
  existingTransactionId: number;
  missingAccountId: number;
  description?: string;
  notes?: string;
  transactionDate?: string;
}

export interface BulkConfirmTransfersRequest {
  confirmations: ConfirmTransferMatchRequest[];
}

export interface ConfirmTransfersResponse {
  success: boolean;
  message: string;
  transfersCreated: number;
  transactionsUpdated: number;
  errors: string[];
}