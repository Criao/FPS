const express = require('express');
const cors = require('cors');
const mysql = require('mysql2/promise');
const dbConfig = require('./db-config');
const { readInt } = require('./env');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const app = express();
const PORT = readInt('PORT', 3000);
const TOKEN_TTL_MS = readInt('TOKEN_TTL_DAYS', 7) * 24 * 60 * 60 * 1000;
const RESET_CODE_TTL_MS = readInt('RESET_CODE_TTL_MINUTES', 15) * 60 * 1000;
const PBKDF2_ITERATIONS = readInt('PBKDF2_ITERATIONS', 100000);
const UPDATE_MANIFEST_PATH = path.join(__dirname, 'public/updates/manifest.json');
const RESET_REQUEST_MESSAGE = 'If an account exists for that email, a reset code has been sent.';

app.use(cors());
app.use(express.json());

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

        await ensureDatabaseSchema();
    } catch (error) {
        console.error('Database connection failed:', error.message);
        process.exit(1);
    }
}

async function ensureDatabaseSchema() {
    await pool.query(`
        CREATE TABLE IF NOT EXISTS users (
            id INT AUTO_INCREMENT PRIMARY KEY,
            user_id VARCHAR(50) NOT NULL,
            username VARCHAR(50) NOT NULL,
            email VARCHAR(100) NOT NULL,
            password VARCHAR(255) NOT NULL,
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            UNIQUE KEY uniq_users_user_id (user_id),
            UNIQUE KEY uniq_users_username (username),
            UNIQUE KEY uniq_users_email (email),
            INDEX idx_username (username),
            INDEX idx_email (email)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    `);

    await pool.query(`
        CREATE TABLE IF NOT EXISTS guests (
            id INT AUTO_INCREMENT PRIMARY KEY,
            user_id VARCHAR(50) NOT NULL,
            username VARCHAR(50) NOT NULL,
            device_id VARCHAR(100) NOT NULL,
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            UNIQUE KEY uniq_guests_user_id (user_id),
            UNIQUE KEY uniq_guests_device_id (device_id),
            INDEX idx_device_id (device_id)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    `);

    await pool.query(`
        CREATE TABLE IF NOT EXISTS sessions (
            id INT AUTO_INCREMENT PRIMARY KEY,
            token_hash VARCHAR(64) UNIQUE NOT NULL,
            user_id VARCHAR(50) NOT NULL,
            is_guest TINYINT(1) NOT NULL DEFAULT 0,
            expires_at DATETIME NOT NULL,
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            INDEX idx_token_hash (token_hash),
            INDEX idx_user_id (user_id),
            INDEX idx_expires_at (expires_at)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    `);

    await pool.query(`
        CREATE TABLE IF NOT EXISTS password_reset_tokens (
            id INT AUTO_INCREMENT PRIMARY KEY,
            email VARCHAR(100) NOT NULL,
            code_hash VARCHAR(64) NOT NULL,
            expires_at DATETIME NOT NULL,
            used_at DATETIME NULL,
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            INDEX idx_email (email),
            INDEX idx_code_hash (code_hash),
            INDEX idx_expires_at (expires_at)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    `);

    await ensureUniqueIndex('users', 'uniq_users_user_id', 'user_id');
    await ensureUniqueIndex('users', 'uniq_users_username', 'username');
    await ensureUniqueIndex('users', 'uniq_users_email', 'email');
    await ensureUniqueIndex('guests', 'uniq_guests_user_id', 'user_id');
    await ensureUniqueIndex('guests', 'uniq_guests_device_id', 'device_id');
}

async function ensureUniqueIndex(tableName, indexName, columnName) {
    const [existing] = await pool.query(
        `
        SELECT 1
        FROM information_schema.statistics
        WHERE table_schema = ?
          AND table_name = ?
          AND (
            index_name = ?
            OR (column_name = ? AND non_unique = 0)
          )
        LIMIT 1
        `,
        [dbConfig.database, tableName, indexName, columnName]
    );

    if (existing.length > 0) {
        return;
    }

    await pool.query(`ALTER TABLE \`${tableName}\` ADD UNIQUE INDEX \`${indexName}\` (\`${columnName}\`)`);
}

// Helper functions
function generateToken() {
    return crypto.randomBytes(32).toString('hex');
}

function generateUserId() {
    return 'user_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
}

function sha256(value) {
    return crypto.createHash('sha256').update(value).digest('hex');
}

function hashPassword(passwordSecret) {
    const salt = crypto.randomBytes(16).toString('hex');
    const hash = crypto.pbkdf2Sync(passwordSecret, salt, PBKDF2_ITERATIONS, 32, 'sha256').toString('hex');
    return `pbkdf2$${PBKDF2_ITERATIONS}$${salt}$${hash}`;
}

function verifyPassword(passwordSecret, storedPassword) {
    if (!storedPassword) {
        return false;
    }

    const parts = storedPassword.split('$');
    if (parts.length === 4 && parts[0] === 'pbkdf2') {
        const iterations = Number(parts[1]);
        const salt = parts[2];
        const expectedHash = parts[3];
        const actualHash = crypto.pbkdf2Sync(passwordSecret, salt, iterations, 32, 'sha256').toString('hex');
        if (actualHash.length !== expectedHash.length) {
            return false;
        }

        return crypto.timingSafeEqual(Buffer.from(actualHash, 'hex'), Buffer.from(expectedHash, 'hex'));
    }

    // Backward compatibility for existing accounts that stored the client SHA256 directly.
    return storedPassword === passwordSecret;
}

function getBearerToken(req) {
    const authorization = req.headers.authorization || '';
    if (authorization.startsWith('Bearer ')) {
        return authorization.substring('Bearer '.length).trim();
    }

    return req.body?.token || '';
}

function readUpdateManifest() {
    const manifestJson = fs.readFileSync(UPDATE_MANIFEST_PATH, 'utf8');
    return JSON.parse(manifestJson);
}

function getManifestTotalSize(manifest) {
    if (typeof manifest.totalSize === 'number') {
        return manifest.totalSize;
    }

    if (!Array.isArray(manifest.bundles)) {
        return 0;
    }

    return manifest.bundles.reduce((total, bundle) => total + (Number(bundle.size) || 0), 0);
}

function getPublicBaseUrl(req) {
    if (process.env.PUBLIC_BASE_URL) {
        return process.env.PUBLIC_BASE_URL.replace(/\/$/, '');
    }

    return `${req.protocol}://${req.get('host')}`;
}

function normalizeString(value) {
    return typeof value === 'string' ? value.trim() : '';
}

function normalizeUsername(value) {
    return normalizeString(value);
}

function normalizeEmail(value) {
    return normalizeString(value).toLowerCase();
}

function getPasswordSecret(req) {
    return normalizeString(req.body?.passwordHash || req.body?.password);
}

function isValidUsername(username) {
    return username.length >= 3 && username.length <= 50;
}

function isValidEmail(email) {
    return email.length <= 100 && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
}

function isValidPasswordSecret(passwordSecret) {
    return passwordSecret.length >= 6 && passwordSecret.length <= 255;
}

function isValidDeviceId(deviceId) {
    return deviceId.length > 0 && deviceId.length <= 100;
}

async function createSession(userId, isGuest) {
    await pool.query('DELETE FROM sessions WHERE expires_at <= NOW()');

    const token = generateToken();
    const tokenHash = sha256(token);
    const expiresAt = new Date(Date.now() + TOKEN_TTL_MS);

    await pool.query(
        'INSERT INTO sessions (token_hash, user_id, is_guest, expires_at) VALUES (?, ?, ?, ?)',
        [tokenHash, userId, isGuest ? 1 : 0, expiresAt]
    );

    return {
        token,
        expiresAt
    };
}

async function getUserData(userId, isGuest, token, expiresAt) {
    if (isGuest) {
        const [guests] = await pool.query(
            'SELECT * FROM guests WHERE user_id = ?',
            [userId]
        );

        if (guests.length === 0) {
            return null;
        }

        return {
            userId: guests[0].user_id,
            username: guests[0].username,
            token,
            tokenExpireTime: new Date(expiresAt).getTime(),
            isGuest: true
        };
    }

    const [users] = await pool.query(
        'SELECT * FROM users WHERE user_id = ?',
        [userId]
    );

    if (users.length === 0) {
        return null;
    }

    return {
        userId: users[0].user_id,
        username: users[0].username,
        email: users[0].email,
        token,
        tokenExpireTime: new Date(expiresAt).getTime(),
        isGuest: false
    };
}

// Register endpoint
app.post('/api/auth/register', async (req, res) => {
    const username = normalizeUsername(req.body?.username);
    const email = normalizeEmail(req.body?.email);
    const finalPassword = getPasswordSecret(req);

    if (!username || !email || !finalPassword) {
        return res.json({
            success: false,
            message: 'Missing required fields'
        });
    }

    if (!isValidUsername(username)) {
        return res.json({
            success: false,
            message: 'Username must be 3-50 characters'
        });
    }

    if (!isValidEmail(email)) {
        return res.json({
            success: false,
            message: 'Invalid email format'
        });
    }

    if (!isValidPasswordSecret(finalPassword)) {
        return res.json({
            success: false,
            message: 'Invalid password'
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
        const storedPassword = hashPassword(finalPassword);
        await pool.query(
            'INSERT INTO users (user_id, username, email, password) VALUES (?, ?, ?, ?)',
            [userId, username, email, storedPassword]
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
    const username = normalizeUsername(req.body?.username);
    const finalPassword = getPasswordSecret(req);

    if (!username || !finalPassword) {
        return res.json({
            success: false,
            message: 'Missing username or password'
        });
    }

    try {
        const [users] = await pool.query(
            'SELECT * FROM users WHERE username = ?',
            [username]
        );

        if (users.length === 0 || !verifyPassword(finalPassword, users[0].password)) {
            return res.json({
                success: false,
                message: 'Invalid username or password'
            });
        }

        const user = users[0];
        const session = await createSession(user.user_id, false);

        if (!user.password.startsWith('pbkdf2$')) {
            await pool.query(
                'UPDATE users SET password = ? WHERE id = ?',
                [hashPassword(finalPassword), user.id]
            );
        }

        console.log(`[Login] User logged in: ${username}`);

        res.json({
            success: true,
            message: 'Login successful',
            data: {
                userId: user.user_id,
                username: user.username,
                email: user.email,
                token: session.token,
                tokenExpireTime: session.expiresAt.getTime(),
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
    const deviceId = normalizeString(req.body?.deviceId);

    if (!isValidDeviceId(deviceId)) {
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

        const session = await createSession(guest.user_id, true);

        console.log(`[Guest Login] Guest logged in: ${guest.username}`);

        res.json({
            success: true,
            message: 'Guest login successful',
            data: {
                userId: guest.user_id,
                username: guest.username,
                token: session.token,
                tokenExpireTime: session.expiresAt.getTime(),
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

// Verify saved token endpoint
app.post('/api/auth/verify', async (req, res) => {
    const token = getBearerToken(req);

    if (!token) {
        return res.json({
            success: false,
            message: 'Missing token'
        });
    }

    try {
        const [sessions] = await pool.query(
            'SELECT * FROM sessions WHERE token_hash = ? AND expires_at > NOW()',
            [sha256(token)]
        );

        if (sessions.length === 0) {
            return res.json({
                success: false,
                message: 'Token expired or invalid'
            });
        }

        const session = sessions[0];
        const userData = await getUserData(session.user_id, !!session.is_guest, token, session.expires_at);

        if (!userData) {
            await pool.query('DELETE FROM sessions WHERE id = ?', [session.id]);
            return res.json({
                success: false,
                message: 'User not found'
            });
        }

        res.json({
            success: true,
            message: 'Token valid',
            data: userData
        });
    } catch (error) {
        console.error('[Verify Token] Error:', error);
        res.json({
            success: false,
            message: 'Token verification failed'
        });
    }
});

// Logout endpoint
app.post('/api/auth/logout', async (req, res) => {
    const token = getBearerToken(req);

    try {
        if (token) {
            await pool.query('DELETE FROM sessions WHERE token_hash = ?', [sha256(token)]);
        }

        res.json({
            success: true,
            message: 'Logged out'
        });
    } catch (error) {
        console.error('[Logout] Error:', error);
        res.json({
            success: false,
            message: 'Logout failed'
        });
    }
});

// Forgot password endpoint
app.post('/api/auth/forgot-password', async (req, res) => {
    const email = normalizeEmail(req.body?.email);

    if (!email) {
        return res.json({
            success: false,
            message: 'Missing email address'
        });
    }

    if (!isValidEmail(email)) {
        return res.json({
            success: false,
            message: 'Invalid email format'
        });
    }

    try {
        const [users] = await pool.query(
            'SELECT * FROM users WHERE email = ?',
            [email]
        );

        if (users.length === 0) {
            return res.json({
                success: true,
                message: RESET_REQUEST_MESSAGE
            });
        }

        console.log(`[Forgot Password] Password reset requested for: ${email}`);

        const resetCode = crypto.randomInt(100000, 1000000).toString();
        const expiresAt = new Date(Date.now() + RESET_CODE_TTL_MS);

        await pool.query(
            'UPDATE password_reset_tokens SET used_at = NOW() WHERE email = ? AND used_at IS NULL',
            [email]
        );

        await pool.query(
            'INSERT INTO password_reset_tokens (email, code_hash, expires_at) VALUES (?, ?, ?)',
            [email, sha256(resetCode), expiresAt]
        );

        console.log(`[Forgot Password] Reset code for ${email}: ${resetCode}`);

        res.json({
            success: true,
            message: RESET_REQUEST_MESSAGE
        });
    } catch (error) {
        console.error('[Forgot Password] Error:', error);
        res.json({
            success: false,
            message: 'Password reset failed'
        });
    }
});

// Reset password endpoint
app.post('/api/auth/reset-password', async (req, res) => {
    const email = normalizeEmail(req.body?.email);
    const resetCode = normalizeString(req.body?.resetCode);
    const finalPassword = getPasswordSecret(req);

    if (!email || !resetCode || !finalPassword) {
        return res.json({
            success: false,
            message: 'Missing required fields'
        });
    }

    if (!isValidEmail(email) || !isValidPasswordSecret(finalPassword)) {
        return res.json({
            success: false,
            message: 'Invalid reset request'
        });
    }

    try {
        const [resetRows] = await pool.query(
            'SELECT * FROM password_reset_tokens WHERE email = ? AND code_hash = ? AND used_at IS NULL AND expires_at > NOW() ORDER BY created_at DESC LIMIT 1',
            [email, sha256(resetCode)]
        );

        if (resetRows.length === 0) {
            return res.json({
                success: false,
                message: 'Invalid or expired reset code'
            });
        }

        const [users] = await pool.query(
            'SELECT * FROM users WHERE email = ?',
            [email]
        );

        if (users.length === 0) {
            return res.json({
                success: false,
                message: 'Invalid or expired reset code'
            });
        }

        await pool.query(
            'UPDATE users SET password = ? WHERE email = ?',
            [hashPassword(finalPassword), email]
        );

        await pool.query(
            'UPDATE password_reset_tokens SET used_at = NOW() WHERE id = ?',
            [resetRows[0].id]
        );

        await pool.query(
            'DELETE FROM sessions WHERE user_id = ? AND is_guest = 0',
            [users[0].user_id]
        );

        res.json({
            success: true,
            message: 'Password reset successful'
        });
    } catch (error) {
        console.error('[Reset Password] Error:', error);
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

    try {
        const manifest = readUpdateManifest();

        res.json({
            success: true,
            data: {
                version: manifest.version,
                buildNumber: manifest.buildNumber,
                catalogUrl: `${getPublicBaseUrl(req)}/updates/manifest.json`,
                totalSize: getManifestTotalSize(manifest),
                forceUpdate: !!manifest.forceUpdate,
                updateDescription: manifest.updateDescription || ''
            }
        });
    } catch (error) {
        console.error('[Version Check] Error:', error);
        res.json({
            success: false,
            message: 'Version manifest unavailable'
        });
    }
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
