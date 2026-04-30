import os
from dotenv import load_dotenv

load_dotenv()

DB_SERVER = os.getenv("DB_SERVER", "localhost")
DB_NAME   = os.getenv("DB_NAME",   "StudySyncDB")
DB_DRIVER = os.getenv("DB_DRIVER", "ODBC Driver 17 for SQL Server")

# Build the SQLAlchemy connection string for SQL Server using Windows Authentication
CONNECTION_STRING = (
    f"mssql+pyodbc://@{DB_SERVER}/{DB_NAME}"
    f"?driver={DB_DRIVER.replace(' ', '+')}"
    f"&trusted_connection=yes"
)

# How many hours before a cached recommendation expires
CACHE_EXPIRY_HOURS = 24

# Number of top matches to store per user
TOP_N_MATCHES = 10

# Minimum cosine similarity score to store (0.0 to 1.0)
MIN_SCORE_THRESHOLD = 0.01