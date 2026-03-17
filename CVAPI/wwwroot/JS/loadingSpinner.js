// Loading Spinner Utility
// Shows until process completes naturally

class LoadingSpinner {
    constructor() {
        this.createSpinnerModal();
        this.attachEventListeners();
        this.setupAutoHide();
    }

    createSpinnerModal() {
        // Create loading overlay
        const overlay = document.createElement('div');
        overlay.id = 'loadingOverlay';
        overlay.style.cssText = `
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background-color: rgba(0, 0, 0, 0.2);
            display: none;
            z-index: 9999;
            justify-content: center;
            align-items: center;
        `;

        // Create spinner container
        const spinnerContainer = document.createElement('div');
        spinnerContainer.style.cssText = `
            background: white;
            padding: 15px;
            border-radius: 6px;
            text-align: center;
            box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
        `;

        // Create spinner
        const spinner = document.createElement('div');
        spinner.style.cssText = `
            border: 2px solid #f3f3f3;
            border-top: 2px solid #00534C;
            border-radius: 50%;
            width: 24px;
            height: 24px;
            animation: spin 0.6s linear infinite;
            margin: 0 auto 8px auto;
        `;

        // Add CSS animation
        const style = document.createElement('style');
        style.textContent = `
            @keyframes spin {
                0% { transform: rotate(0deg); }
                100% { transform: rotate(360deg); }
            }
        `;
        document.head.appendChild(style);

        // Create loading text
        const loadingText = document.createElement('p');
        loadingText.textContent = 'Processing...';
        loadingText.style.cssText = `
            margin: 0;
            font-family: 'Roboto', sans-serif;
            color: #333;
            font-size: 12px;
        `;

        // Assemble the modal
        spinnerContainer.appendChild(spinner);
        spinnerContainer.appendChild(loadingText);
        overlay.appendChild(spinnerContainer);
        document.body.appendChild(overlay);
    }

    show() {
        const overlay = document.getElementById('loadingOverlay');
        if (overlay) {
            overlay.style.display = 'flex';
        }
    }

    hide() {
        const overlay = document.getElementById('loadingOverlay');
        if (overlay) {
            overlay.style.display = 'none';
        }
    }

    setupAutoHide() {
        // Hide when page starts to unload (form submission is happening)
        window.addEventListener('beforeunload', () => {
            this.hide();
        });

        // Hide when page becomes hidden (navigation started)
        document.addEventListener('visibilitychange', () => {
            if (document.hidden) {
                this.hide();
            }
        });

        // Hide when page finishes loading (if still showing)
        window.addEventListener('load', () => {
            this.hide();
        });

        // Hide when DOM content changes (likely server response)
        const observer = new MutationObserver((mutations) => {
            // Only hide if significant changes happened (not just our spinner)
            const significantChange = mutations.some(mutation => 
                mutation.addedNodes.length > 0 && 
                Array.from(mutation.addedNodes).some(node => 
                    node.nodeType === Node.ELEMENT_NODE && 
                    !node.id?.includes('loadingOverlay')
                )
            );
            
            if (significantChange) {
                setTimeout(() => this.hide(), 100); // Small delay to let changes settle
            }
        });
        
        observer.observe(document.body, {
            childList: true,
            subtree: true
        });

        // Failsafe timeout (only as backup)
        this.timeoutId = null;
    }

    attachEventListeners() {
        // Wait for DOM to be ready
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => this.bindButtons());
        } else {
            this.bindButtons();
        }
    }

    bindButtons() {
        // Show spinner on submit button clicks
        document.addEventListener('click', (e) => {
            const target = e.target;
            
            // Only for submit buttons, not modal buttons
            if ((target.tagName === 'BUTTON' && target.type === 'submit') ||
                (target.tagName === 'INPUT' && target.type === 'submit')) {
                
                // Skip modal buttons
                if (target.classList.contains('error-modal-button') ||
                    target.classList.contains('alert-modal-button') ||
                    target.classList.contains('confirm-modal-button')) {
                    return;
                }
                
                // Show spinner
                this.show();
                
                // Set a backup timeout only as failsafe (in case nothing else hides it)
                if (this.timeoutId) {
                    clearTimeout(this.timeoutId);
                }
                this.timeoutId = setTimeout(() => {
                    this.hide();
                }, 8000); // 8 seconds as absolute maximum
            }
        }, true);
    }

    // Public methods
    static show() {
        window.loadingSpinnerInstance?.show();
    }

    static hide() {
        window.loadingSpinnerInstance?.hide();
    }
}

// Initialize when script loads
document.addEventListener('DOMContentLoaded', function() {
    window.loadingSpinnerInstance = new LoadingSpinner();
});

// Also initialize immediately if DOM is already ready
if (document.readyState !== 'loading') {
    window.loadingSpinnerInstance = new LoadingSpinner();
}