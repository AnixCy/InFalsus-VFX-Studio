namespace InFalsus.Vfx.Addon;

/// <summary>Bound dimensions before asking Unity to allocate a decoded image.</summary>
public static class ImageHeader
{
    public static void Validate(byte[] data)
    {
        if(data.Length>16*1024*1024)throw new InvalidDataException("背景图片超过 16 MiB。");
        int U16(int p)=>(data[p]<<8)|data[p+1];
        int U32(int p)=>(data[p]<<24)|(data[p+1]<<16)|(data[p+2]<<8)|data[p+3];
        int width=0,height=0;
        if(data.Length>=24&&data[0]==137&&data[1]==80&&data[2]==78&&data[3]==71&&data[12]==73&&data[13]==72&&data[14]==68&&data[15]==82) {
            width=U32(16);height=U32(20);
        } else if(data.Length>=4&&data[0]==255&&data[1]==216) {
            int p=2;
            while(p+3<data.Length) {
                if(data[p++]!=255)break;while(p<data.Length&&data[p]==255)p++;
                if(p>=data.Length)break;int marker=data[p++];
                if(marker is 0xd9 or 0xda)break;
                if(marker==1||marker>=0xd0&&marker<=0xd7)continue;
                if(p+2>data.Length)break;int length=U16(p);if(length<2||p+length>data.Length)break;
                if(marker>=0xc0&&marker<=0xcf&&marker is not (0xc4 or 0xc8 or 0xcc)) {
                    if(length<7)break;height=U16(p+3);width=U16(p+5);break;
                }
                p+=length;
            }
        }
        if(width<=0||height<=0||width>4096||height>4096)throw new InvalidDataException("背景须为有效 PNG/JPEG，宽高不超过 4096 像素。");
    }
}
