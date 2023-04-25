namespace CustomDatabase


type IDataStorage =
    // TODO: interface of singleton responsible for storing/retrieving data for different entities
    // entity table per file?
    // hashtable: pointer->file?
    // what files to load on get? (i.e. if only ids are needed for filtering, first choose ids, then open files)
    // entity pointer should somehow contain both entity type and entity id
    abstract member1: int -> int
