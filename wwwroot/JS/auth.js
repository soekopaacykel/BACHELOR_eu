// Global authentication interceptor for VEXA application
// Automatically adds JWT tokens to API requests and handles authentication

class AuthManager {
    constructor() {
        this.initializeAuthInterceptor();
    }

    // Get JWT token from localStorage
    getToken() {
        return localStorage.getItem('jwt_token');
    }

    // Get user role from localStorage
    getUserRole() {
        return localStorage.getItem('userRole');
    }

    // Get user email from localStorage
    getUserEmail() {
        return localStorage.getItem('userEmail');
    }

    // Get user ID from localStorage
    getUserId() {
        return localStorage.getItem('userId');
    }

    // Check if user is authenticated
    isAuthenticated() {
        const token = this.getToken();
        console.log('[AUTH] Checking authentication, token present:', !!token);

        if (!token) {
            console.log('[AUTH] No token found');
            return false;
        }

        try {
            // Decode JWT payload to check expiration
            const payload = JSON.parse(atob(token.split('.')[1]));
            const currentTime = Math.floor(Date.now() / 1000);

            console.log('[AUTH] Token payload:', payload);
            console.log('[AUTH] Token expires at:', payload.exp);
            console.log('[AUTH] Current time:', currentTime);

            // Check if token is expired
            if (payload.exp && payload.exp < currentTime) {
                console.log('[AUTH] Token expired, clearing localStorage');
                this.logout();
                return false;
            }

            console.log('[AUTH] Token is valid');
            return true;
        } catch (error) {
            console.error('[AUTH] Invalid token format:', error);
            console.error('[AUTH] Token was:', token);
            this.logout();
            return false;
        }
    }

    // Check if user has required role
    hasRole(requiredRoles) {
        if (!this.isAuthenticated()) return false;

        const userRole = this.getUserRole();
        if (Array.isArray(requiredRoles)) {
            return requiredRoles.includes(userRole);
        }
        return userRole === requiredRoles;
    }

    // Logout user and clear all stored data
    logout() {
        localStorage.removeItem('jwt_token');
        localStorage.removeItem('userRole');
        localStorage.removeItem('userEmail');
        localStorage.removeItem('userId');
        localStorage.removeItem('adminInitials');
        window.location.href = '/';
    }

    // Redirect to login if not authenticated
    requireAuth() {
        if (!this.isAuthenticated()) {
            console.log('Authentication required, redirecting to login');
            window.location.href = '/';
            return false;
        }
        return true;
    }

    // Initialize fetch interceptor
    initializeAuthInterceptor() {
        // Store original fetch
        const originalFetch = window.fetch;

        // Override fetch to automatically add Authorization header
        window.fetch = (url, options = {}) => {
            const token = this.getToken();

            // Only add auth header for API calls or if token exists
            if (token && (url.includes('/api/') || options.requireAuth)) {
                // Initialize headers if not present
                options.headers = options.headers || {};

                // For FormData requests (file uploads), don't set Content-Type
                if (options.body instanceof FormData) {
                    options.headers['Authorization'] = `Bearer ${token}`;
                } else {
                    // For regular requests
                    options.headers = {
                        ...options.headers,
                        'Authorization': `Bearer ${token}`,
                        'Content-Type': options.headers['Content-Type'] || 'application/json'
                    };
                }

                console.log(`[AUTH] Adding token to request: ${url}`);
            }

            // Call original fetch
            return originalFetch(url, options)
                .then(response => {
                    // Handle unauthorized responses
                    if (response.status === 401) {
                        console.log('Unauthorized response, clearing authentication');
                        this.logout();
                        return response;
                    }

                    return response;
                })
                .catch(error => {
                    console.error('Fetch error:', error);
                    throw error;
                });
        };

        console.log('[AUTH] Authentication interceptor initialized');
    }
}

// Create global auth manager instance
window.authManager = new AuthManager();

// Convenience functions for backward compatibility
function getJwtToken() {
    return window.authManager.getToken();
}

function isAuthenticated() {
    return window.authManager.isAuthenticated();
}

function hasRole(requiredRoles) {
    return window.authManager.hasRole(requiredRoles);
}

function requireAuth() {
    return window.authManager.requireAuth();
}

function logout() {
    window.authManager.logout();
}

// Initialize auth on page load
document.addEventListener('DOMContentLoaded', function () {
    console.log('[AUTH] Auth manager loaded');
    console.log('[AUTH] Current path:', window.location.pathname);
    console.log('[AUTH] localStorage contents:', {
        jwt_token: localStorage.getItem('jwt_token') ? 'Present' : 'Missing',
        userRole: localStorage.getItem('userRole'),
        userEmail: localStorage.getItem('userEmail')
    });

    // Display user email in any element with id="userEmail"
    setTimeout(() => {
        displayUserEmail();
    }, 50); // Small delay to ensure page elements are ready

    // Check authentication on protected pages
    const currentPath = window.location.pathname.toLowerCase();
    const publicPages = ['/', '/index', '/login'];

    console.log('[AUTH] Is public page?', publicPages.includes(currentPath));
    console.log('[AUTH] Is authenticated?', window.authManager.isAuthenticated());

    if (!publicPages.includes(currentPath) && !window.authManager.isAuthenticated()) {
        console.log('[AUTH] Protected page accessed without authentication - redirecting to login');
        // Give a small delay to ensure localStorage is fully loaded
        setTimeout(() => {
            if (!window.authManager.isAuthenticated()) {
                window.authManager.logout();
            }
        }, 100);
    } else {
        console.log('[AUTH] Authentication check passed');
    }
});

// Function to display user email from localStorage
function displayUserEmail() {
    const userEmail = localStorage.getItem('userEmail');
    const userEmailElement = document.getElementById('userEmail');

    console.log('[AUTH] displayUserEmail called:', {
        userEmail: userEmail,
        element: !!userEmailElement,
        elementId: userEmailElement?.id
    });

    if (userEmailElement && userEmail) {
        userEmailElement.textContent = userEmail;
        console.log('[AUTH] Successfully displayed user email:', userEmail);
    } else if (userEmailElement && !userEmail) {
        console.log('[AUTH] Element found but no email in localStorage');
        userEmailElement.textContent = 'Email not found';
    } else if (!userEmailElement) {
        console.log('[AUTH] No userEmail element found on this page');
    }
}

// Make displayUserEmail globally available
window.displayUserEmail = displayUserEmail;

// Export for ES6 modules if needed
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { AuthManager, getJwtToken, isAuthenticated, hasRole, requireAuth, logout };
}
