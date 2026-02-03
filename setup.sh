#!/usr/bin/env bash
# =============================================================================
# MyMascada - Interactive Setup Script
# =============================================================================
# Guides you through configuring MyMascada for self-hosting.
# Usage: ./setup.sh
# =============================================================================

set -euo pipefail

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m' # No Color

# Helpers
info()    { echo -e "${BLUE}[INFO]${NC} $1"; }
success() { echo -e "${GREEN}[OK]${NC} $1"; }
warn()    { echo -e "${YELLOW}[WARN]${NC} $1"; }
error()   { echo -e "${RED}[ERROR]${NC} $1"; }
header()  { echo -e "\n${BOLD}${CYAN}$1${NC}\n"; }

prompt() {
    local var_name="$1"
    local prompt_text="$2"
    local default="${3:-}"
    local value

    if [[ -n "$default" ]]; then
        read -rp "$(echo -e "${BOLD}$prompt_text${NC} [${default}]: ")" value
        value="${value:-$default}"
    else
        read -rp "$(echo -e "${BOLD}$prompt_text${NC}: ")" value
    fi
    eval "$var_name='$value'"
}

prompt_secret() {
    local var_name="$1"
    local prompt_text="$2"
    local value

    read -srp "$(echo -e "${BOLD}$prompt_text${NC}: ")" value
    echo
    eval "$var_name='$value'"
}

prompt_yes_no() {
    local prompt_text="$1"
    local default="${2:-n}"
    local value

    if [[ "$default" == "y" ]]; then
        read -rp "$(echo -e "${BOLD}$prompt_text${NC} [Y/n]: ")" value
        value="${value:-y}"
    else
        read -rp "$(echo -e "${BOLD}$prompt_text${NC} [y/N]: ")" value
        value="${value:-n}"
    fi

    [[ "$value" =~ ^[Yy] ]]
}

generate_password() {
    openssl rand -base64 32 | tr -d '/+=' | head -c 32
}

generate_jwt_key() {
    openssl rand -base64 64 | tr -d '\n'
}

# =============================================================================
# Preflight checks
# =============================================================================
header "MyMascada - Self-Hosting Setup"

echo -e "This script will guide you through configuring MyMascada."
echo -e "You can re-run it at any time to update your configuration.\n"

# Check Docker
if ! command -v docker &> /dev/null; then
    error "Docker is not installed. Please install Docker first:"
    echo "  https://docs.docker.com/get-docker/"
    exit 1
fi
success "Docker found: $(docker --version)"

# Check Docker Compose v2
if docker compose version &> /dev/null; then
    success "Docker Compose found: $(docker compose version --short)"
else
    error "Docker Compose v2 is not available."
    echo "  Please update Docker or install the compose plugin:"
    echo "  https://docs.docker.com/compose/install/"
    exit 1
fi

# Check openssl
if ! command -v openssl &> /dev/null; then
    error "openssl is not installed. It's needed to generate secure secrets."
    exit 1
fi
success "openssl found"

# =============================================================================
# Load existing .env if present
# =============================================================================
ENV_FILE=".env"
if [[ -f "$ENV_FILE" ]]; then
    warn "Existing .env file found."
    if prompt_yes_no "Would you like to reconfigure? (existing values will be used as defaults)"; then
        # shellcheck disable=SC1090
        source "$ENV_FILE"
        info "Loaded existing configuration as defaults."
    else
        info "Keeping existing configuration."
        echo ""
        if prompt_yes_no "Start the application now?"; then
            docker compose up -d --build
            echo ""
            success "MyMascada is starting! Check status with: docker compose ps"
        fi
        exit 0
    fi
fi

# =============================================================================
# Required: Database
# =============================================================================
header "1/6 - Database Configuration"

info "PostgreSQL will be set up automatically via Docker."

prompt DB_USER "Database username" "${DB_USER:-mymascada}"
prompt DB_NAME "Database name" "${DB_NAME:-mymascada}"

if [[ "${DB_PASSWORD:-CHANGE_ME}" == "CHANGE_ME" ]]; then
    DB_PASSWORD=$(generate_password)
    success "Generated secure database password."
else
    if prompt_yes_no "Keep existing database password?"; then
        info "Keeping existing password."
    else
        DB_PASSWORD=$(generate_password)
        success "Generated new database password."
    fi
fi

# =============================================================================
# Required: JWT
# =============================================================================
header "2/6 - JWT Authentication"

if [[ "${JWT_KEY:-}" == *"CHANGE_ME"* ]] || [[ -z "${JWT_KEY:-}" ]]; then
    JWT_KEY=$(generate_jwt_key)
    success "Generated secure JWT signing key."
else
    if prompt_yes_no "Keep existing JWT key?"; then
        info "Keeping existing key."
    else
        JWT_KEY=$(generate_jwt_key)
        success "Generated new JWT key."
    fi
fi

JWT_ISSUER="${JWT_ISSUER:-MyMascada}"
JWT_AUDIENCE="${JWT_AUDIENCE:-MyMascadaUsers}"

# =============================================================================
# Required: Application URLs
# =============================================================================
header "3/6 - Application URLs"

prompt FRONTEND_URL "Frontend URL (where users access the app)" "${FRONTEND_URL:-http://localhost:3000}"
API_URL="${API_URL:-http://api:5126}"
CORS_ALLOWED_ORIGINS="${FRONTEND_URL}"

# Extract host port from FRONTEND_URL for docker-compose port mapping
FRONTEND_PORT=$(echo "${FRONTEND_URL}" | sed -E 's|https?://[^:]+:?||' | sed 's|/.*||')
if [ -z "$FRONTEND_PORT" ]; then
  # No port in URL — default to 80 for http, 443 for https
  case "$FRONTEND_URL" in
    https://*) FRONTEND_PORT=443 ;;
    *) FRONTEND_PORT=80 ;;
  esac
fi

# Set PUBLIC_API_URL (the URL browsers use to reach the API)
# For localhost, this is the direct API port; for domains with a reverse proxy,
# the proxy routes /api/* so PUBLIC_API_URL should match FRONTEND_URL.
if [[ "$FRONTEND_URL" == *"localhost"* ]]; then
    PUBLIC_API_URL="${PUBLIC_API_URL:-http://localhost:5126}"
else
    PUBLIC_API_URL="${PUBLIC_API_URL:-${FRONTEND_URL}}"
    info "Reverse proxy detected. PUBLIC_API_URL set to: ${PUBLIC_API_URL}"
fi

info "CORS will allow requests from: ${CORS_ALLOWED_ORIGINS}"

# =============================================================================
# Optional: AI Categorization
# =============================================================================
header "4/6 - AI Categorization (Optional)"

echo -e "MyMascada can use OpenAI to automatically categorize your transactions."
echo -e "Without this, you can still categorize manually or use rule-based matching.\n"

if prompt_yes_no "Enable AI categorization?"; then
    prompt OPENAI_API_KEY "OpenAI API key" "${OPENAI_API_KEY:-}"
    prompt OPENAI_MODEL "OpenAI model" "${OPENAI_MODEL:-gpt-4o-mini}"

    if [[ -z "$OPENAI_API_KEY" ]]; then
        warn "No API key provided. AI categorization will be disabled."
    else
        success "AI categorization configured with model: $OPENAI_MODEL"
    fi
else
    OPENAI_API_KEY=""
    OPENAI_MODEL="gpt-4o-mini"
    info "AI categorization disabled. You can enable it later by updating .env."
fi

# =============================================================================
# Optional: Google OAuth
# =============================================================================
header "5/6 - Google OAuth (Optional)"

echo -e "Enable 'Sign in with Google' alongside email/password authentication.\n"

GOOGLE_CLIENT_ID="${GOOGLE_CLIENT_ID:-}"
GOOGLE_CLIENT_SECRET="${GOOGLE_CLIENT_SECRET:-}"

if prompt_yes_no "Enable Google OAuth?"; then
    prompt GOOGLE_CLIENT_ID "Google Client ID" "$GOOGLE_CLIENT_ID"
    prompt_secret GOOGLE_CLIENT_SECRET "Google Client Secret"

    if [[ -n "$GOOGLE_CLIENT_ID" ]] && [[ -n "$GOOGLE_CLIENT_SECRET" ]]; then
        success "Google OAuth configured."
    else
        warn "Incomplete credentials. Google OAuth will be disabled."
        GOOGLE_CLIENT_ID=""
        GOOGLE_CLIENT_SECRET=""
    fi
else
    info "Google OAuth disabled. Email/password authentication is always available."
fi

# =============================================================================
# Optional: Email
# =============================================================================
header "6/6 - Email Notifications (Optional)"

echo -e "Email enables password reset, email verification, and notifications."
echo -e "Without it, the app still works but some features are limited.\n"

EMAIL_ENABLED="${EMAIL_ENABLED:-false}"
EMAIL_PROVIDER="${EMAIL_PROVIDER:-smtp}"
EMAIL_FROM_ADDRESS="${EMAIL_FROM_ADDRESS:-noreply@example.com}"
EMAIL_FROM_NAME="${EMAIL_FROM_NAME:-MyMascada}"
SMTP_HOST="${SMTP_HOST:-}"
SMTP_PORT="${SMTP_PORT:-587}"
SMTP_USERNAME="${SMTP_USERNAME:-}"
SMTP_PASSWORD="${SMTP_PASSWORD:-}"
SMTP_USE_STARTTLS="${SMTP_USE_STARTTLS:-true}"
SMTP_USE_SSL="${SMTP_USE_SSL:-false}"

if prompt_yes_no "Enable email notifications?"; then
    EMAIL_ENABLED="true"
    prompt EMAIL_FROM_ADDRESS "From email address" "$EMAIL_FROM_ADDRESS"
    prompt EMAIL_FROM_NAME "From display name" "$EMAIL_FROM_NAME"
    prompt SMTP_HOST "SMTP host" "$SMTP_HOST"
    prompt SMTP_PORT "SMTP port" "$SMTP_PORT"
    prompt SMTP_USERNAME "SMTP username" "$SMTP_USERNAME"
    prompt_secret SMTP_PASSWORD "SMTP password"
    success "Email configured via SMTP."
else
    info "Email disabled. You can enable it later by updating .env."
fi

# =============================================================================
# Optional features not prompted (can be set manually in .env)
# =============================================================================
AKAHU_ENABLED="${AKAHU_ENABLED:-false}"
AKAHU_APP_SECRET="${AKAHU_APP_SECRET:-}"
BETA_REQUIRE_INVITE_CODE="${BETA_REQUIRE_INVITE_CODE:-false}"
BETA_VALID_INVITE_CODES="${BETA_VALID_INVITE_CODES:-}"

# =============================================================================
# Write .env file
# =============================================================================
header "Writing configuration..."

cat > "$ENV_FILE" << ENVEOF
# =============================================================================
# MyMascada - Generated Configuration
# Generated on: $(date -u +"%Y-%m-%d %H:%M:%S UTC")
# Re-run ./setup.sh to reconfigure.
# =============================================================================

# Database
DB_USER=${DB_USER}
DB_PASSWORD=${DB_PASSWORD}
DB_NAME=${DB_NAME}

# JWT Authentication
JWT_KEY=${JWT_KEY}
JWT_ISSUER=${JWT_ISSUER}
JWT_AUDIENCE=${JWT_AUDIENCE}

# Application URLs
FRONTEND_URL=${FRONTEND_URL}
FRONTEND_PORT=${FRONTEND_PORT}
API_URL=${API_URL}
PUBLIC_API_URL=${PUBLIC_API_URL}
CORS_ALLOWED_ORIGINS=${CORS_ALLOWED_ORIGINS}

# Beta Access
BETA_REQUIRE_INVITE_CODE=${BETA_REQUIRE_INVITE_CODE}
BETA_VALID_INVITE_CODES=${BETA_VALID_INVITE_CODES}

# AI Categorization (OpenAI)
OPENAI_API_KEY=${OPENAI_API_KEY}
OPENAI_MODEL=${OPENAI_MODEL}

# Google OAuth
GOOGLE_CLIENT_ID=${GOOGLE_CLIENT_ID}
GOOGLE_CLIENT_SECRET=${GOOGLE_CLIENT_SECRET}

# Bank Sync (Akahu - NZ only)
AKAHU_ENABLED=${AKAHU_ENABLED}
AKAHU_APP_SECRET=${AKAHU_APP_SECRET}

# Email
EMAIL_ENABLED=${EMAIL_ENABLED}
EMAIL_PROVIDER=${EMAIL_PROVIDER}
EMAIL_FROM_ADDRESS=${EMAIL_FROM_ADDRESS}
EMAIL_FROM_NAME=${EMAIL_FROM_NAME}
SMTP_HOST=${SMTP_HOST}
SMTP_PORT=${SMTP_PORT}
SMTP_USERNAME=${SMTP_USERNAME}
SMTP_PASSWORD=${SMTP_PASSWORD}
SMTP_USE_STARTTLS=${SMTP_USE_STARTTLS}
SMTP_USE_SSL=${SMTP_USE_SSL}
ENVEOF

chmod 600 "$ENV_FILE"
success "Configuration written to .env (permissions set to 600)"

# =============================================================================
# Validate
# =============================================================================
header "Validating configuration..."

if docker compose config --quiet 2>/dev/null; then
    success "Docker Compose configuration is valid."
else
    error "Docker Compose configuration has errors. Please check .env and docker-compose.yml"
    exit 1
fi

# =============================================================================
# Summary
# =============================================================================
header "Configuration Summary"

echo -e "  ${BOLD}Database:${NC}           PostgreSQL (${DB_USER}@postgres/${DB_NAME})"
echo -e "  ${BOLD}Frontend URL:${NC}       ${FRONTEND_URL}"
echo -e "  ${BOLD}API URL:${NC}            ${API_URL}"

if [[ -n "$OPENAI_API_KEY" ]]; then
    echo -e "  ${BOLD}AI Categorization:${NC}  ${GREEN}Enabled${NC} (${OPENAI_MODEL})"
else
    echo -e "  ${BOLD}AI Categorization:${NC}  ${YELLOW}Disabled${NC}"
fi

if [[ -n "$GOOGLE_CLIENT_ID" ]] && [[ -n "$GOOGLE_CLIENT_SECRET" ]]; then
    echo -e "  ${BOLD}Google OAuth:${NC}       ${GREEN}Enabled${NC}"
else
    echo -e "  ${BOLD}Google OAuth:${NC}       ${YELLOW}Disabled${NC}"
fi

if [[ "$EMAIL_ENABLED" == "true" ]]; then
    echo -e "  ${BOLD}Email:${NC}              ${GREEN}Enabled${NC} (${SMTP_HOST}:${SMTP_PORT})"
else
    echo -e "  ${BOLD}Email:${NC}              ${YELLOW}Disabled${NC}"
fi

if [[ "$AKAHU_ENABLED" == "true" ]]; then
    echo -e "  ${BOLD}Bank Sync:${NC}          ${GREEN}Enabled${NC} (Akahu)"
else
    echo -e "  ${BOLD}Bank Sync:${NC}          ${YELLOW}Disabled${NC}"
fi

echo ""

# =============================================================================
# Launch
# =============================================================================
if prompt_yes_no "Start MyMascada now?" "y"; then
    echo ""
    info "Building and starting containers..."
    docker compose up -d --build

    echo ""
    info "Waiting for services to start..."
    sleep 5

    # Check health
    if docker compose ps --format json 2>/dev/null | grep -q '"Health":"healthy"'; then
        echo ""
        success "MyMascada is running!"
        echo ""
        echo -e "  ${BOLD}Application:${NC}  ${FRONTEND_URL}"
        echo -e "  ${BOLD}API Health:${NC}   http://localhost:5126/health"
        echo ""
        echo -e "  Useful commands:"
        echo -e "    docker compose ps        - Check service status"
        echo -e "    docker compose logs -f   - View logs"
        echo -e "    docker compose down      - Stop all services"
        echo -e "    docker compose up -d     - Start services"
        echo ""
    else
        warn "Services are starting up. This may take a minute for the first build."
        echo ""
        echo -e "  Check status with:  docker compose ps"
        echo -e "  View logs with:     docker compose logs -f"
        echo ""
    fi
else
    echo ""
    info "To start later, run:"
    echo "  docker compose up -d --build"
    echo ""
fi
