// Region Management Utility
class RegionManager {
    static SUPPORTED_REGIONS = {
        'DK': 'Denmark',
        'VN': 'Vietnam'
    };
    
    static DEFAULT_REGION = 'DK';
    
    // Get current region from localStorage - NO fallback to DK
    static getCurrentRegion() {
        const stored = localStorage.getItem('selectedRegion');
        return stored && RegionManager.SUPPORTED_REGIONS[stored] ? stored : null;
    }
    
    // Set current region in localStorage
    static setCurrentRegion(region) {
        if (RegionManager.SUPPORTED_REGIONS[region]) {
            localStorage.setItem('selectedRegion', region);
            // Trigger a custom event for other components to listen to
            window.dispatchEvent(new CustomEvent('regionChanged', { detail: { region } }));
            return true;
        }
        return false;
    }
    
    // Get region display name
    static getRegionName(region) {
        return RegionManager.SUPPORTED_REGIONS[region] || 'Unknown';
    }
    
    // Get all supported regions
    static getAllRegions() {
        return Object.entries(RegionManager.SUPPORTED_REGIONS).map(([code, name]) => ({
            code,
            name
        }));
    }
    
    // Create region selector HTML
    static createRegionSelector(containerId, onChangeCallback = null) {
        const container = document.getElementById(containerId);
        if (!container) return;
        
        const currentRegion = RegionManager.getCurrentRegion();
        
        const selector = document.createElement('div');
        selector.className = 'region-selector';
        selector.innerHTML = `
            <div class="form-group">
                <label for="regionSelect" class="form-label">Region:</label>
                <select id="regionSelect" class="form-select">
                    ${RegionManager.getAllRegions().map(region => 
                        `<option value="${region.code}" ${region.code === currentRegion ? 'selected' : ''}>${region.name}</option>`
                    ).join('')}
                </select>
            </div>
        `;
        
        container.appendChild(selector);
        
        // Add event listener for changes
        const selectElement = selector.querySelector('#regionSelect');
        selectElement.addEventListener('change', (e) => {
            const newRegion = e.target.value;
            RegionManager.setCurrentRegion(newRegion);
            if (onChangeCallback) {
                onChangeCallback(newRegion);
            }
        });
        
        return selector;
    }
    
    // Update API URLs to include region
    static updateApiUrl(baseUrl, region = null) {
        const currentRegion = region || RegionManager.getCurrentRegion();
        if (!currentRegion) {
            throw new Error('No region specified and no region found in localStorage. Please select a region first.');
        }
        return baseUrl.replace('{region}', currentRegion);
    }
}

// Make it globally available
window.RegionManager = RegionManager;