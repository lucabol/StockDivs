import yfinance as yf
from typing import Annotated, Union
from pydantic import BaseModel
from fastapi import FastAPI, Header, HTTPException
from typing import Union, List, Optional
import pandas as pd
import os
import json

app = FastAPI()

apiKey1 = os.getenv('API_KEY1')
apiKey2 = os.getenv('API_KEY2')
apiKeyPresent = apiKey1 is not None or apiKey2 is not None


class InvokeRequest(BaseModel):
    symbol: str
    method: str
    # Optional params list of ints or strings, defaults to None
    params: dict = None

    model_config = {
        "json_schema_extra": {
            "examples": [
                {
                    "symbol": "MSFT",
                    "method": "info"
                },
                {
                    "symbol": "AAPL",
                    "method": "history",
                    "params": {
                        "period": "1mo"
                    }
                }
            ]
        }
    }

class StocksRequest(BaseModel):
    symbols: list[str]

    model_config = {
        "json_schema_extra": {
            "examples": [
                {
                    "symbols": ["MSFT", "RKT.L"]
                }
            ]
        }
    }

@app.get("/health")
async def read_health():
    return "Ok"

@app.get("/api/version")
async def read_root():
    yfinanceVersion = yf.__version__
    return {"hello": "world", "now": pd.Timestamp.now(), "yfinanceVersion": yfinanceVersion}

@app.get("/api/{ticker}/info")
async def read_info(ticker):
    t = yf.Ticker(ticker)
    return t.info

# https://stackoverflow.com/questions/63107594/how-to-deal-with-multi-level-column-names-downloaded-with-yfinance/63107801#63107801
def flatten_index(df, key):
    # Just keep the column with the key
    df = df[[key]]
    # Flatten the column index
    df.columns = [col[1] for col in df.columns]
    # Eliminate nan values
    df = df.bfill().ffill()
    return df

@app.post("/api/quotes")
async def read_quotes(request: StocksRequest):
    df = yf.download(request.symbols, period="1d", ignore_tz=True)
    df = flatten_index(df, "Close")
    d = df.head(1).to_dict(orient='series')
    for k,v in d.items():
        d[k] = v.iloc[-1]
    return d

@app.post("/api/dividends")
async def read_divs(request: StocksRequest):
    df = yf.download(request.symbols, period="2y", actions=True)
    df = flatten_index(df, "Dividends")
    d = df.to_dict(orient='series')
    # Each element in the dictionary is a pandas series of (date, dividend) pairs
    # Remove each element in the series that is 0
    for k, v in d.items():
        d[k] = v[v != 0]
    return d

@app.post("/api/invoke")
async def read_item(request: InvokeRequest, x_api_key: Annotated[str | None, Header()] = None):
    if apiKeyPresent:
        if x_api_key is None:
            raise HTTPException(
                status_code=401, detail="API key is missing"
            )
        if x_api_key != apiKey1 and x_api_key != apiKey2:
            raise HTTPException(
                status_code=403, detail="API key is invalid"
            )
        
        
    ticker = yf.Ticker(request.symbol)
    method = getattr(ticker, request.method, None)

    if method is None:
        raise HTTPException( status_code=404, detail="Method not found")

    if isinstance(method, dict):
        return method

    if isinstance(method, pd.Series): 
        return method.to_dict()

    if isinstance(method, pd.DataFrame): 
        return method.to_dict(orient='series')

    if request.params is None:
        result = method()
    else:
        result = method(**request.params)

    if isinstance(result, dict):
        return result

    if isinstance(result, pd.Series): 
        return result.to_dict()

    return result.to_dict(orient='records')