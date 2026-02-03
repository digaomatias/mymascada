export interface LoginRequest {
  emailOrUserName: string;
  password: string;
  rememberMe: boolean;
}

export interface RegisterRequest {
  email: string;
  userName: string;
  password: string;
  confirmPassword: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  currency: string;
  timeZone: string;
  inviteCode?: string;
}

export interface UserDto {
  id: string;
  email: string;
  userName: string;
  firstName: string;
  lastName: string;
  fullName: string;
  currency: string;
  timeZone: string;
  locale: string;
  profilePictureUrl?: string;
}

export interface AuthenticationResponse {
  isSuccess: boolean;
  token?: string;
  expiresAt?: string;
  user?: UserDto;
  errors: string[];
  requiresEmailVerification?: boolean;
  message?: string;
}

// Email Verification Types
export interface ConfirmEmailRequest {
  email: string;
  token: string;
}

export interface ConfirmEmailResponse {
  success: boolean;
  message: string;
}

export interface ResendVerificationRequest {
  email: string;
}

export interface ResendVerificationResponse {
  success: boolean;
  message: string;
}

export interface ApiResponse<T = unknown> {
  isSuccess: boolean;
  data?: T;
  errors: string[];
}

// Password Reset Types
export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
  confirmPassword: string;
}

export interface PasswordResetResponse {
  isSuccess: boolean;
  message: string;
  errors: string[];
}