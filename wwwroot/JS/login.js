document.querySelector('#login-form').addEventListener('submit', async function (event) {
    event.preventDefault();

    const email = document.querySelector('#email').value;
    const password = document.querySelector('#password').value;
    
    // Always use DK region
    const region = 'DK';

    try {
        const response = await fetch(`/api/user/${region}/login`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                email: email,
                password: password
            })
        });

        if (response.ok) {
            const data = await response.json();
            if (data.token) {
                // Store JWT token in localStorage only
                localStorage.setItem('jwt_token', data.token);
                localStorage.setItem('userRole', data.userRole);
                localStorage.setItem('userEmail', data.email || email);
                localStorage.setItem('userId', data.userId);
                
                // Store admin initials if present
                if (data.adminInitials) {
                    localStorage.setItem('adminInitials', data.adminInitials);
                }
                
                console.log('Login successful:', {
                    token: data.token ? 'Token received' : 'No token',
                    role: data.userRole,
                    email: data.email || email
                });
                window.location.href = "/consultants";
            } else {
                alert('Login failed: No token received');
            }
        } else {
            const data = await response.json();
            alert(data.message || 'Login failed');
        }
    } catch (error) {
        console.error('Login error:', error);
        alert('An error occurred during login');
    }
});

// Helper to get JWT token from localStorage
function getJwtTokenFromLocalStorage() {
    if (window.localStorage) {
        return localStorage.getItem('jwt_token');
    }
    return null;
}

document.querySelector('form[asp-page-handler="SaveNote"]').addEventListener('submit', function (event) {
    event.preventDefault();

    const form = event.target;
    const formData = new FormData(form);

    // Retrieve the JWT token from localStorage
    const jwtToken = getJwtTokenFromLocalStorage();

    console.log('[DEBUG] Retrieved JWT Token:', jwtToken); // Log the token for debugging

    if (!jwtToken) {
        alert('Authorization token is missing. Please log in again.');
        return;
    }

    // Convert form data to JSON
    const requestData = Object.fromEntries(formData.entries());
    console.log('[DEBUG] Request Data:', requestData); // Log the request data

    // Get current region from RegionManager or default to DK
    const region = window.RegionManager ? window.RegionManager.getCurrentRegion() : 'DK';
    const apiUrl = `/api/user/${region}/save-private-note`;

    fetch(apiUrl, {
        method: 'POST',
        headers: {
            'Authorization': `Bearer ${jwtToken}`, // Include the JWT token in the Authorization header
            'Content-Type': 'application/json', // Ensure JSON content type
        },
        body: JSON.stringify(requestData), // Send JSON data
    })
        .then(response => {
            if (response.ok) {
                console.log('[DEBUG] Note saved successfully.');
                window.location.reload();
            } else {
                response.text().then(error => {
                    console.error('[ERROR] Backend Response:', error);
                    alert(`Failed to save note: ${error}`);
                });
            }
        })
        .catch(error => {
            console.error('[ERROR] Network Error:', error);
            alert('An error occurred. Please try again.');
        });
});
