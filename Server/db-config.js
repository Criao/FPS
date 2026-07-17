const { loadEnv, readInt } = require('./env');

loadEnv();

module.exports = {
    host: process.env.DB_HOST || 'localhost',
    port: readInt('DB_PORT', 3306),
    user: process.env.DB_USER || 'root',
    password: process.env.DB_PASSWORD || '123456',
    database: process.env.DB_NAME || 'fps_game',
    waitForConnections: true,
    connectionLimit: readInt('DB_CONNECTION_LIMIT', 10),
    queueLimit: 0
};
