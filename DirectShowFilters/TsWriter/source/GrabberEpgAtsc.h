/*
 *  Copyright (C) 2006-2008 Team MediaPortal
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
#include <map>
#include <streams.h>    // CUnknown, LPUNKNOWN
#include <vector>
#include <WinError.h>   // HRESULT
#include "..\..\shared\ISectionCallback.h"
#include "..\..\shared\SectionDecoder.h"
#include "..\..\shared\TsHeader.h"
#include "CriticalSection.h"
#include "ICallBackGrabber.h"
#include "ICallBackPidConsumer.h"
#include "IGrabberEpgAtsc.h"
#include "ParserEitAtsc.h"
#include "ParserEtt.h"

using namespace MediaPortal;
using namespace std;


#define PID_EIT_ATSC_CALL_BACK        TABLE_ID_EIT_ATSC
#define TABLE_ID_EIT_ATSC_CALL_BACK   TABLE_ID_EIT_ATSC


class CGrabberEpgAtsc
  : public CUnknown, ICallBackTableParser, public IGrabberEpgAtsc
{
  public:
    CGrabberEpgAtsc(ICallBackPidConsumer* callBack, LPUNKNOWN unk, HRESULT* hr);
    virtual ~CGrabberEpgAtsc(void);

    DECLARE_IUNKNOWN

    STDMETHODIMP NonDelegatingQueryInterface(REFIID iid, void** ppv);

    void AddEitDecoders(const vector<unsigned short>& pids);
    void RemoveEitDecoders(const vector<unsigned short>& pids);
    void AddEttDecoders(const vector<unsigned short>& pids);
    void RemoveEttDecoders(const vector<unsigned short>& pids);

    STDMETHODIMP_(void) Start();
    STDMETHODIMP_(void) Stop();

    void Reset(bool enableCrcCheck);
    STDMETHODIMP_(void) SetCallBack(ICallBackGrabber* callBack);
    bool OnTsPacket(CTsHeader& header, unsigned char* tsPacket);
    STDMETHODIMP_(bool) IsSeen();
    STDMETHODIMP_(bool) IsReady();

    STDMETHODIMP_(unsigned long) GetEventCount();
    STDMETHODIMP_(bool) GetEvent(unsigned long index,
                                  unsigned short* sourceId,
                                  unsigned short* eventId,
                                  unsigned long long* startDateTime,
                                  unsigned short* duration,
                                  unsigned char* textCount,
                                  unsigned long* audioLanguages,
                                  unsigned char* audioLanguageCount,
                                  unsigned long* captionsLanguages,
                                  unsigned char* captionsLanguageCount,
                                  unsigned char* genreIds,
                                  unsigned char* genreIdCount,
                                  unsigned char* vchipRating,
                                  unsigned char* mpaaClassification,
                                  unsigned short* advisory);
    STDMETHODIMP_(bool) GetEventTextByIndex(unsigned long eventIndex,
                                            unsigned char textIndex,
                                            unsigned long* language,
                                            char* title,
                                            unsigned short* titleBufferSize,
                                            char* text,
                                            unsigned short* textBufferSize);
    STDMETHODIMP_(bool) GetEventTextByLanguage(unsigned long eventIndex,
                                                unsigned long language,
                                                char* title,
                                                unsigned short* titleBufferSize,
                                                char* text,
                                                unsigned short* textBufferSize);

  private:
    bool SelectEventByIndex(unsigned long index);

    void OnTableSeen(unsigned char tableId);
    void OnTableComplete(unsigned char tableId);
    void OnTableChange(unsigned char tableId);

    CCriticalSection m_section;
    bool m_isGrabbing;
    bool m_isSeen;
    bool m_isReady;
    ICallBackGrabber* m_callBackGrabber;
    ICallBackPidConsumer* m_callBackPidConsumer;
    map<unsigned short, CParserEitAtsc*> m_parsersEit;
    map<unsigned short, CParserEtt*> m_parsersEtt;
    bool m_enableCrcCheck;

    CParserEitAtsc* m_currentEventParser;
    unsigned long m_currentEventIndex;
    unsigned long m_currentEventIndexOffset;
    unsigned short m_currentEventId;
    unsigned short m_currentEventSourceId;
};