#!/usr/bin/env python3
"""
Test script for ShareData2 API
"""

import requests
import json
import time

def test_api():
    base_url = "http://localhost:53868"
    
    print("Testing ShareData2 API...")
    print("=" * 50)
    
    # Test root endpoint
    try:
        response = requests.get(f"{base_url}/", timeout=5)
        print(f"Root endpoint: {response.status_code}")
        if response.status_code == 200:
            data = response.json()
            print(f"Response: {json.dumps(data, indent=2)}")
    except requests.exceptions.RequestException as e:
        print(f"Root endpoint failed: {e}")
    
    print()
    
    # Test getData endpoint (partial)
    try:
        response = requests.get(f"{base_url}/getData?type=partial", timeout=5)
        print(f"GetData (partial): {response.status_code}")
        if response.status_code == 200:
            data = response.json()
            print(f"Game State: {data.get('gameState', 'Unknown')}")
            print(f"Area Name: {data.get('areaName', 'Unknown')}")
            print(f"Is Loading: {data.get('isLoading', 'Unknown')}")
            print(f"Player Level: {data.get('player', {}).get('level', 'Unknown')}")
    except requests.exceptions.RequestException as e:
        print(f"GetData (partial) failed: {e}")
    
    print()
    
    # Test getData endpoint (full)
    try:
        response = requests.get(f"{base_url}/getData?type=full", timeout=5)
        print(f"GetData (full): {response.status_code}")
        if response.status_code == 200:
            data = response.json()
            terrain_length = len(data.get('terrainString', ''))
            print(f"Terrain String Length: {terrain_length}")
    except requests.exceptions.RequestException as e:
        print(f"GetData (full) failed: {e}")
    
    print()
    
    # Test getScreenPos endpoint
    try:
        response = requests.get(f"{base_url}/getScreenPos?x=100&y=200", timeout=5)
        print(f"GetScreenPos: {response.status_code}")
        if response.status_code == 200:
            data = response.json()
            print(f"Screen Position: {data}")
    except requests.exceptions.RequestException as e:
        print(f"GetScreenPos failed: {e}")
    
    print()
    print("API test completed!")

if __name__ == "__main__":
    test_api()
