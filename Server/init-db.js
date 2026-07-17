const mysql = require('mysql2/promise');
const dbConfig = require('./db-config');

async function initDatabase() {
    try {
        if (!/^[A-Za-z0-9_]+$/.test(dbConfig.database)) {
            throw new Error('Invalid database name');
        }

        // Connect without database first
        const connection = await mysql.createConnection({
            host: dbConfig.host,
            port: dbConfig.port,
            user: dbConfig.user,
            password: dbConfig.password
        });

        console.log('Connected to MySQL server');

        // Create database
        await connection.query(`CREATE DATABASE IF NOT EXISTS ${dbConfig.database} CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci`);
        console.log(`Database '${dbConfig.database}' created or already exists`);

        // Use the database
        await connection.query(`USE ${dbConfig.database}`);

        // Create users table
        await connection.query(`
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
        console.log('Table "users" created or already exists');

        // Create guests table
        await connection.query(`
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
        console.log('Table "guests" created or already exists');

        // Create sessions table
        await connection.query(`
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
        console.log('Table "sessions" created or already exists');

        // Create password reset token table
        await connection.query(`
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
        console.log('Table "password_reset_tokens" created or already exists');

        await ensureUniqueIndex(connection, 'users', 'uniq_users_user_id', 'user_id');
        await ensureUniqueIndex(connection, 'users', 'uniq_users_username', 'username');
        await ensureUniqueIndex(connection, 'users', 'uniq_users_email', 'email');
        await ensureUniqueIndex(connection, 'guests', 'uniq_guests_user_id', 'user_id');
        await ensureUniqueIndex(connection, 'guests', 'uniq_guests_device_id', 'device_id');

        await connection.end();
        console.log('\nDatabase initialization completed successfully!');
    } catch (error) {
        console.error('Database initialization failed:', error.message);
        process.exit(1);
    }
}

async function ensureUniqueIndex(connection, tableName, indexName, columnName) {
    const [existing] = await connection.query(
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

    await connection.query(`ALTER TABLE \`${tableName}\` ADD UNIQUE INDEX \`${indexName}\` (\`${columnName}\`)`);
    console.log(`Unique index "${indexName}" created`);
}

initDatabase();
