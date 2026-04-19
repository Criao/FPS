const express = require('express');
const bodyParser = require('body-parser');
const cors = require('cors');
const mysql = require('mysql2/promise');
const dbConfig = require('./db-config');
const path = require('path');

const app = express();
const PORT = 3000;

// Middleware
app.use(cors());
app.use(bodyParser.json());

// 静态文件服务 - 用于提供 AssetBundle 下载
app.use('/updates', express.static(path.join(__dirname, 'public/updates')));

// Database connection pool
let pool;

// Initialize database connection
async function initDatabase() {
    try {
        pool = mysql.createPool(dbConfig);
        console.log('Database connection pool created');

        // Test connection
        const connection = await pool.getConnection();
        console.log('Database connected successfully');
        connection.release();
    } catch (error) {
        console.error('Database connection failed:', error.message);
        process.exit(1);
    }
}

// Helper functions
function generateToken() {
    return 'token_' + Math.random().toString(36).substr(2, 9) + Date.now();
}

function generateUserId() {
    return 'user_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
}

// Register endpoint
app.post('/api/auth/register', async (req, res) => {
    const { username, email, password, passwordHash } = req.body;

    // Accept either password or passwordHash
    const finalPassword = passwordHash || password;

    if (!username || !email || !finalPassword) {
        return res.json({
            success: false,
            message: 'Missing required fields'
        });
    }

    try {
        // Check if username or email already exists
        const [existing] = await pool.query(
            'SELECT * FROM users WHERE username = ? OR email = ?',
            [username, email]
        );

        if (existing.length > 0) {
            return res.json({
                success: false,
                message: 'Username or email already exists'
            });
        }

        // Create new user
        const userId = generateUserId();
        await pool.query(
            'INSERT INTO users (user_id, username, email, password) VALUES (?, ?, ?, ?)',
            [userId, username, email, finalPassword]
        );

        console.log(`[Register] New user: ${username}`);

        res.json({
            success: true,
            message: 'Registration successful'
        });
    } catch (error) {
        console.error('[Register] Error:', error);
        res.json({
            success: false,
            message: 'Registration failed'
        });
    }
});

// Login endpoint
app.post('/api/auth/login', async (req, res) => {
    const { username, password, passwordHash } = req.body;

    // Accept either password or passwordHash
    const finalPassword = passwordHash || password;

    if (!username || !finalPassword) {
        return res.json({
            success: false,
            message: 'Missing username or password'
        });
    }

    try {
        const [users] = await pool.query(
            'SELECT * FROM users WHERE username = ? AND password = ?',
            [username, finalPassword]
        );

        if (users.length === 0) {
            return res.json({
                success: false,
                message: 'Invalid username or password'
            });
        }

        const user = users[0];
        const token = generateToken();

        console.log(`[Login] User logged in: ${username}`);

        res.json({
            success: true,
            message: 'Login successful',
            data: {
                userId: user.user_id,
                username: user.username,
                email: user.email,
                token: token,
                isGuest: false
            }
        });
    } catch (error) {
        console.error('[Login] Error:', error);
        res.json({
            success: false,
            message: 'Login failed'
        });
    }
});

// Guest login endpoint
app.post('/api/auth/guest', async (req, res) => {
    const { deviceId } = req.body;

    if (!deviceId) {
        return res.json({
            success: false,
            message: 'Missing device ID'
        });
    }

    try {
        // Check if guest already exists
        const [guests] = await pool.query(
            'SELECT * FROM guests WHERE device_id = ?',
            [deviceId]
        );

        let guest;
        if (guests.length > 0) {
            guest = guests[0];
        } else {
            // Create new guest
            const userId = generateUserId();
            const username = 'Guest_' + Math.random().toString(36).substr(2, 6);

            await pool.query(
                'INSERT INTO guests (user_id, username, device_id) VALUES (?, ?, ?)',
                [userId, username, deviceId]
            );

            guest = {
                user_id: userId,
                username: username,
                device_id: deviceId
            };
        }

        const token = generateToken();

        console.log(`[Guest Login] Guest logged in: ${guest.username}`);

        res.json({
            success: true,
            message: 'Guest login successful',
            data: {
                userId: guest.user_id,
                username: guest.username,
                token: token,
                isGuest: true
            }
        });
    } catch (error) {
        console.error('[Guest Login] Error:', error);
        res.json({
            success: false,
            message: 'Guest login failed'
        });
    }
});

// Forgot password endpoint
app.post('/api/auth/forgot-password', async (req, res) => {
    const { email } = req.body;

    if (!email) {
        return res.json({
            success: false,
            message: 'Missing email address'
        });
    }

    try {
        const [users] = await pool.query(
            'SELECT * FROM users WHERE email = ?',
            [email]
        );

        if (users.length === 0) {
            return res.json({
                success: false,
                message: 'Email not found'
            });
        }

        console.log(`[Forgot Password] Password reset requested for: ${email}`);

        // In production, send email here
        res.json({
            success: true,
            message: 'Password reset email sent'
        });
    } catch (error) {
        console.error('[Forgot Password] Error:', error);
        res.json({
            success: false,
            message: 'Password reset failed'
        });
    }
});

// Version check endpoint
app.get('/api/version/check', (req, res) => {
    const { platform, currentVersion } = req.query;

    console.log(`[Version Check] Platform: ${platform}, Version: ${currentVersion}`);

    // Return new version available
    res.json({
        success: true,
        data: {
            version: '1.1.0',
            buildNumber: 2,
            catalogUrl: 'http://localhost:3000/updates/manifest.json',
            totalSize: 10485760, // 10MB
            forceUpdate: false,
            updateDescription: 'Bug fixes and performance improvements'
        }
    });
});

// Start server
async function startServer() {
    await initDatabase();

    app.listen(PORT, () => {
        console.log(`=================================`);
        console.log(`FPS Game Auth Server (MySQL)`);
        console.log(`Running on http://localhost:${PORT}`);
        console.log(`Database: ${dbConfig.database}`);
        console.log(`=================================`);
    });
}

startServer();
