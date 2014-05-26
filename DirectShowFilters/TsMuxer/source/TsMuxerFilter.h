/* 
 *  Copyright (C) 2005-2013 Team MediaPortal
 *  http://www.team-mediaportal.com
 *
 *  This Program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2, or (at your option)
 *  any later version.
 *   
 *  This Program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *   
 *  You should have received a copy of the GNU General Public License
 *  along with GNU Make; see the file COPYING.  If not, write to
 *  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA. 
 *  http://www.gnu.org/copyleft/gpl.html
 *
 */
#pragma once
#include <streams.h>
#include <InitGuid.h>
#include <Windows.h>
#include <vector>
#include "IStreamMultiplexer.h"
#include "MuxInputPin.h"
#include "TsOutputPin.h"

using namespace std;


#define STREAM_IDLE_TIMEOUT 1000


// {511d13f0-8a56-42fa-b151-b72a325cf71a}
DEFINE_GUID(CLSID_TS_MUXER, 0x511d13f0, 0x8a56, 0x42fa, 0xb1, 0x51, 0xb7, 0x2a, 0x32, 0x5c, 0xf7, 0x1a);

class CTsMuxerFilter : public CBaseFilter
{
  public:
    CTsMuxerFilter(IStreamMultiplexer* multiplexer, wchar_t* debugPath, LPUNKNOWN unk, CCritSec* filterLock, CCritSec* receiveLock, HRESULT* hr);
    virtual ~CTsMuxerFilter(void);

    CBasePin* GetPin(int n);
    HRESULT AddPin();
    int GetPinCount();
    HRESULT Deliver(PBYTE data, long dataLength);

    STDMETHODIMP Pause();
    STDMETHODIMP Run(REFERENCE_TIME startTime);
    STDMETHODIMP Stop();

    static void __cdecl StreamingMonitorThreadFunction(void* arg);

    STDMETHODIMP SetDumpFilePath(wchar_t* path);
    STDMETHODIMP DumpInput(long mask);
    STDMETHODIMP DumpOutput(bool enable);

  private:
    IStreamMultiplexer* m_multiplexer;
    CTsOutputPin* m_outputPin;          // MPEG 2 transport stream output pin
    vector<CMuxInputPin*> m_inputPins;  // input pins
    CCritSec* m_receiveLock;            // sample receive lock

    HANDLE m_streamingMonitorThread;
    HANDLE m_streamingMonitorThreadStopEvent;

    wchar_t m_debugPath[MAX_PATH];
    long m_inputPinDebugMask;
    bool m_isOutputDebugEnabled;
};