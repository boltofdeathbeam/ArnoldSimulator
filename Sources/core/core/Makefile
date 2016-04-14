#
# Common settings
#
CFLAGS = $(OPTS) -D_CRT_SECURE_NO_WARNINGS -D_SCL_SECURE_NO_WARNINGS
LFLAGS = -language charm++

#
# Project files
#
BIN = core
RUN = charmrun
SRCS = $(patsubst %.cpp,%.cpp,$(wildcard *.cpp))
OBJS = $(patsubst %.cpp,%.o,$(wildcard *.cpp))
DCLS = $(patsubst %.ci,%.decl.h,$(wildcard *.ci))
DEFS = $(patsubst %.ci,%.def.h,$(wildcard *.ci))

#
# Debug build settings
#
DBG_DIR = debug
DBG_BIN = $(DBG_DIR)/$(BIN)
DBG_RUN = $(DBG_DIR)/$(RUN)
DBG_DCLS = $(addprefix $(DBG_DIR)/, $(DCLS))
DBG_DEFS = $(addprefix $(DBG_DIR)/, $(DEFS))
DBG_OBJS = $(addprefix $(DBG_DIR)/, $(OBJS))
DBG_CHARMDIR = ../libs/charm/net-debug
DBG_TBBDIR = ../libs/tbb
DBG_SHASHDIR = ../libs/sparsehash
DBG_JSONDIR = ../libs/json
DBG_CHARMFLAGS = -no-optimize
DBG_CFLAGS = $(CFLAGS) -g -I$(DBG_DIR) -I$(DBG_TBBDIR)/include -I$(DBG_SHASHDIR)/src -I$(DBG_JSONDIR)/src
DBG_LFLAGS = $(LFLAGS) -g -L$(DBG_TBBDIR)/lib -ltbb_debug -ltbbmalloc_debug -ltbbmalloc_proxy_debug
DBG_CHARMC = $(DBG_CHARMDIR)/bin/charmc
DBG_DEPFILE = $(DBG_DIR)/$(*F)
DBG_DEPEND = touch $(DBG_DEPFILE).Td && makedepend $(DBG_CFLAGS) -p $(DBG_DIR)/ -f $(DBG_DEPFILE).Td $< >/dev/null 2>&1

#
# Release build settings
#
RLS_DIR = release
RLS_BIN = $(RLS_DIR)/$(BIN)
RLS_RUN = $(RLS_DIR)/$(RUN)
RLS_DCLS = $(addprefix $(RLS_DIR)/, $(DCLS))
RLS_DEFS = $(addprefix $(RLS_DIR)/, $(DEFS))
RLS_OBJS = $(addprefix $(RLS_DIR)/, $(OBJS))
RLS_CHARMDIR = ../libs/charm/net-release
RLS_TBBDIR = ../libs/tbb
RLS_SHASHDIR = ../libs/sparsehash
RLS_JSONDIR = ../libs/json
RLS_CHARMFLAGS = -optimize -production
RLS_CFLAGS = $(CFLAGS) -I$(RLS_DIR) -I$(RLS_TBBDIR)/include -I$(RLS_SHASHDIR)/src -I$(RLS_JSONDIR)/src
RLS_LFLAGS = $(LFLAGS) -L$(RLS_TBBDIR)/lib -ltbb -ltbbmalloc -ltbbmalloc_proxy
RLS_CHARMC = $(RLS_CHARMDIR)/bin/charmc
RLS_DEPFILE = $(RLS_DIR)/$(*F)
RLS_DEPEND = touch $(RLS_DEPFILE).Td && makedepend $(RLS_CFLAGS) -f $(RLS_DEPFILE).Td $< >/dev/null 2>&1

.PHONY: all clean debug-prep debug-build debug-clean debug-rebuild release-prep release-build release-clean release-rebuild

#
# Default build
#
all: release-build

clean:
	rm -r -f $(DBG_DIR) $(RLS_DIR)

#
# Debug rules
#
debug-build: debug-prep $(DBG_BIN)

debug-prep:
	@mkdir -p $(DBG_DIR)

debug-clean:
	rm -f $(DBG_DCLS) $(DBG_DEFS) $(DBG_OBJS) $(DBG_BIN) $(DBG_RUN)

debug-rebuild: debug-clean debug-build

$(DBG_BIN): $(DBG_DCLS) $(DBG_DEFS) $(DBG_OBJS)
	$(DBG_CHARMC) $(DBG_LFLAGS) -o $@ $(DBG_OBJS)
	@mv -f $(RUN) $(DBG_RUN)
	@cp -f $(DBG_TBBDIR)/bin/tbb_debug.* $(DBG_DIR)
	@cp -f $(DBG_TBBDIR)/bin/tbbmalloc_debug.* $(DBG_DIR)
	@cp -f $(DBG_TBBDIR)/bin/tbbmalloc_proxy_debug.* $(DBG_DIR)

$(DBG_DIR)/%.decl.h: %.ci
	cd $(DBG_DIR) && ../$(DBG_CHARMC) $(DBG_CHARMFLAGS) $(DBG_CFLAGS) ../$<

$(DBG_DIR)/%.def.h: %.ci
	cd $(DBG_DIR) && ../$(DBG_CHARMC) $(DBG_CHARMFLAGS) $(DBG_CFLAGS) ../$<

$(DBG_DIR)/%.o: %.cpp
	@$(DBG_DEPEND); \
	  cp $(DBG_DEPFILE).Td $(DBG_DEPFILE).d; \
	  sed -e 's/#.*//' -e 's/^[^:]*: *//' -e 's/ *\\$$//' \
		  -e '/^$$/ d' -e 's/$$/ :/' < $(DBG_DEPFILE).Td >> $(DBG_DEPFILE).d; \
	  rm -f $(DBG_DEPFILE).Td
	$(DBG_CHARMC) $(DBG_CHARMFLAGS) $(DBG_CFLAGS) -o $@ $<

-include $(SRCS:%.cpp=$(DBG_DIR)/%.d)

#
# Release rules
#
release-build: release-prep $(RLS_BIN)

release-prep:
	@mkdir -p $(RLS_DIR)

release-clean:
	rm -f $(RLS_DCLS) $(RLS_DEFS) $(RLS_OBJS) $(RLS_BIN) $(RLS_RUN)

release-rebuild: release-clean release-build

$(RLS_BIN): $(RLS_DCLS) $(RLS_DEFS) $(RLS_OBJS)
	$(RLS_CHARMC) $(RLS_LFLAGS) -o $@ $(RLS_OBJS)
	@mv -f $(RUN) $(RLS_RUN)
	@cp -f $(RLS_TBBDIR)/bin/tbb.* $(RLS_DIR)
	@cp -f $(RLS_TBBDIR)/bin/tbbmalloc.* $(RLS_DIR)
	@cp -f $(RLS_TBBDIR)/bin/tbbmalloc_proxy.* $(RLS_DIR)

$(RLS_DIR)/%.decl.h: %.ci
	cd $(RLS_DIR) && ../$(RLS_CHARMC) $(RLS_CHARMFLAGS) $(RLS_CFLAGS) ../$<

$(RLS_DIR)/%.def.h: %.ci
	cd $(RLS_DIR) && ../$(RLS_CHARMC) $(RLS_CHARMFLAGS) $(RLS_CFLAGS) ../$<

$(RLS_DIR)/%.o: %.cpp
	@$(RLS_DEPEND); \
	  cp $(RLS_DEPFILE).Td $(RLS_DEPFILE).d; \
	  sed -e 's/#.*//' -e 's/^[^:]*: *//' -e 's/ *\\$$//' \
		  -e '/^$$/ d' -e 's/$$/ :/' < $(RLS_DEPFILE).Td >> $(RLS_DEPFILE).d; \
	  rm -f $(RLS_DEPFILE).Td
	$(RLS_CHARMC) $(RLS_CHARMFLAGS) $(RLS_CFLAGS) -o $@ $<

-include $(SRCS:%.cpp=$(RLS_DIR)/%.d)