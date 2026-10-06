using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ZCompression.App;

// Advertise FileDrop immediately; prepare its files only when a target asks for data.
internal sealed class DeferredArchiveDataObject(IDataObject data, short fileDropFormat, Action prepare) : IDataObject
{
    private bool _prepared;
    private bool _preparing;
    internal Exception? Error { get; private set; }

    private void Prepare(ref FORMATETC format)
    {
        if (format.cfFormat != fileDropFormat) return;
        if (Error is not null) throw new COMException("Archive export failed.", unchecked((int)0x80004004));
        if (_prepared) return;
        if (_preparing) throw new COMException("Archive export is being prepared.", unchecked((int)0x8000000A));
        _preparing = true;
        try { prepare(); _prepared = true; }
        catch (Exception error) { Error = error; throw new COMException("Archive export failed.", unchecked((int)0x80004004)); }
        finally { _preparing = false; }
    }

    public void GetData(ref FORMATETC format, out STGMEDIUM medium) { Prepare(ref format); data.GetData(ref format, out medium); }
    public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium) { Prepare(ref format); data.GetDataHere(ref format, ref medium); }
    public int QueryGetData(ref FORMATETC format) => data.QueryGetData(ref format);
    public int GetCanonicalFormatEtc(ref FORMATETC input, out FORMATETC output) => data.GetCanonicalFormatEtc(ref input, out output);
    public void SetData(ref FORMATETC format, ref STGMEDIUM medium, bool release) => data.SetData(ref format, ref medium, release);
    public IEnumFORMATETC EnumFormatEtc(DATADIR direction) => data.EnumFormatEtc(direction);
    public int DAdvise(ref FORMATETC format, ADVF flags, IAdviseSink sink, out int connection) => data.DAdvise(ref format, flags, sink, out connection);
    public void DUnadvise(int connection) => data.DUnadvise(connection);
    public int EnumDAdvise(out IEnumSTATDATA? enumerator) => data.EnumDAdvise(out enumerator);
}
